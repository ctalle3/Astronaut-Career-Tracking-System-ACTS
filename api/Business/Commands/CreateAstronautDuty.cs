using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

using StargateAPI.Business.Helpers;
using StargateAPI.Business.Enums;


namespace StargateAPI.Business.Commands
{
    public class CreateAstronautDuty : IRequest<CreateAstronautDutyResult>
    {
        public required string Name { get; set; }

        public required string Rank { get; set; }

        public required string DutyTitle { get; set; }

        public required DateTime DutyStartDate { get; set; }
    }

    public class CreateAstronautDutyPreProcessor : IRequestPreProcessor<CreateAstronautDuty>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyPreProcessor(StargateContext context)
        {
            _context = context;
        }

        public async Task Process(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadHttpRequestException("Please enter a valid name.");
            }

            if (string.IsNullOrWhiteSpace(request.Rank))
            {
                throw new BadHttpRequestException("Please enter a valid rank.");
            }

            if (string.IsNullOrWhiteSpace(request.DutyTitle))
            {
                throw new BadHttpRequestException("Please enter a valid duty title.");
            }

            if (request.DutyStartDate == default)
            {
                throw new BadHttpRequestException("Please enter a valid duty start date.");
            }

            var name = request.Name.TrimAndToUpperString();
            var title = request.DutyTitle.TrimAndToUpperString();
            var rank = request.Rank.TrimAndToUpperString();
            
            var isExistingDuty = await _context.AstronautDuties
                .AnyAsync(x => 
                    x.DutyTitle.Trim().ToUpper() == title && 
                    x.DutyStartDate == request.DutyStartDate.Date && 
                    x.Person.Name.Trim().ToUpper() == name &&
                    x.Rank.Trim().ToUpper() == rank, cancellationToken);

            if (isExistingDuty)
            {
                throw new BadHttpRequestException("This duty already exists for this user.");
            }

            var retiredTitle = TitleEnums.RETIRED.ToString();
            
            var isAstronautRetired = _context.AstronautDetails.Any(x =>
                x.Person.Name.ToUpper() == name && x.CurrentDutyTitle.Trim().ToUpper() == retiredTitle);
            
            if (isAstronautRetired && title == retiredTitle)
            {
                throw new BadHttpRequestException("This person is  already retired.");
            }
        }
    }

    public class CreateAstronautDutyHandler : IRequestHandler<CreateAstronautDuty, CreateAstronautDutyResult>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyHandler(StargateContext context)
        {
            _context = context;
        }
        public async Task<CreateAstronautDutyResult> Handle(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            var name = request.Name.TrimAndToUpperString();
            var rank = request.Rank.TrimAndToUpperString();
            var title = request.DutyTitle.TrimAndToUpperString();
            var retiredTitle = TitleEnums.RETIRED.ToString();

            var person = _context.People
                .SingleOrDefault(x => x.Name.ToUpper().Trim() == name);
           
            if (person is null) throw new BadHttpRequestException("There is no astronaut with this name. Please enter another name.");

            var currentDetail = await _context.AstronautDetails
                .SingleOrDefaultAsync(x => x.PersonId == person.Id, cancellationToken: cancellationToken);
            
            if (currentDetail == null && string.Equals(title, retiredTitle, StringComparison.OrdinalIgnoreCase))
                throw new BadHttpRequestException("A person cannot be retired without a previous astronaut duty.");
            
            if (currentDetail == null)
            {
                var newDetail = new AstronautDetail
                {
                    PersonId = person.Id,
                    CurrentDutyTitle =  title,
                    CurrentRank = rank,
                    CareerStartDate = request.DutyStartDate.Date,
                };

                await _context.AstronautDetails.AddAsync(newDetail, cancellationToken);
            }
            
            if(currentDetail != null)
            {
                currentDetail.CurrentDutyTitle = title;
                currentDetail.CurrentRank = rank;
                
                //Rule #6: A Person is classified as 'Retired' when a Duty Title is 'RETIRED'.
                //Rule #7 A Person's Career End Date is one day before the Retired Duty Start Date.
                //This allows for a currently retired astronaut to come out of retirement by setting CareerEndDate to null.
                currentDetail.CareerEndDate = string.Equals(title, retiredTitle, StringComparison.OrdinalIgnoreCase)
                    ? request.DutyStartDate.AddDays(-1).Date : null;
            }

            //Rule #4: A Person's Current Duty will not have a Duty End Date.
            var currentDuty = await _context.AstronautDuties
                .SingleOrDefaultAsync(x => x.PersonId == person.Id && x.DutyEndDate == null, cancellationToken);

            if (currentDuty != null && request.DutyStartDate.Date <= currentDuty.DutyStartDate)
                throw new BadHttpRequestException("New duty must start after the current duty's start date.");

            if (currentDuty != null)
            {
                //Rule #3: A Person will only ever hold one current Astronaut Duty Title, Start Date, and Rank at a time.
                //Rule #5. A Person's Previous Duty End Date is set to the day before the New Astronaut Duty Start Date when a new Astronaut Duty is received for a Person.
                currentDuty.DutyEndDate = request.DutyStartDate.AddDays(-1).Date;
            }

            var newAstronautDuty = new AstronautDuty()
            {
                PersonId = person.Id,
                Rank = rank,
                DutyTitle = title,
                DutyStartDate = request.DutyStartDate.Date,
                //Rule #3: A Person will only ever hold one current Astronaut Duty Title, Start Date, and Rank at a time.
                //Rule #4: A Person's Current Duty will not have a Duty End Date.
                DutyEndDate = null
            };
            
            await _context.AstronautDuties.AddAsync(newAstronautDuty, cancellationToken);
            
            await _context.SaveChangesAsync(cancellationToken);

            return new CreateAstronautDutyResult()
            {
                Id = newAstronautDuty.Id
            };
        }
    }

    public class CreateAstronautDutyResult : BaseResponse
    {
        public int? Id { get; set; }
    }
}