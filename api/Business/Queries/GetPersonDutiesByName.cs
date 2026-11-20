using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Dtos;
using StargateAPI.Business.Helpers;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Queries
{
    public class GetPersonDutiesByName : IRequest<GetAstronautDutiesByNameResult>
    {
        public string Name { get; set; } = string.Empty;
    }

    public class GetAstronautDutiesByNameHandler : IRequestHandler<GetPersonDutiesByName, GetAstronautDutiesByNameResult>
    {
        private readonly StargateContext _context;

        public GetAstronautDutiesByNameHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<GetAstronautDutiesByNameResult> Handle(GetPersonDutiesByName request, CancellationToken cancellationToken)
        {
            var name = request.Name.TrimAndToUpperString();
            
            var astronautDetailAndDuties = await _context.People
                .Include(x => x.AstronautDuties)
                .Include(x => x.AstronautDetail)
                .SingleOrDefaultAsync(x => x.Name.Trim().ToUpper() == name, cancellationToken: cancellationToken);
            
            if (astronautDetailAndDuties == null) throw new BadHttpRequestException("There's no astronaut with that name. Please try another name.");
            
            var detail = astronautDetailAndDuties.AstronautDetail;

            var duties =  astronautDetailAndDuties.AstronautDuties
                    .OrderByDescending(x => x.DutyStartDate)
                    .Select(x => new AstronautDutyDto
                    {
                        Id = x.Id,
                        PersonId = x.PersonId,
                        Rank = x.Rank,
                        DutyTitle = x.DutyTitle,
                        DutyStartDate = x.DutyStartDate,
                        DutyEndDate = x.DutyEndDate
                    })
                    .ToList();
            
            var result = new GetAstronautDutiesByNameResult
            {
                Person = new PersonAstronaut
                {
                    PersonId = astronautDetailAndDuties.Id,
                    Name = astronautDetailAndDuties.Name,
                    CurrentRank = detail?.CurrentRank,
                    CurrentDutyTitle = detail?.CurrentDutyTitle,
                    CareerStartDate = detail?.CareerStartDate,
                    CareerEndDate = detail?.CareerEndDate,
                },
                AstronautDuties = duties
            };

            return result;
        }
    }

    public class GetAstronautDutiesByNameResult : BaseResponse
    {
        public PersonAstronaut Person { get; set; }
        public List<AstronautDutyDto> AstronautDuties { get; set; } = new List<AstronautDutyDto>();
    }
}