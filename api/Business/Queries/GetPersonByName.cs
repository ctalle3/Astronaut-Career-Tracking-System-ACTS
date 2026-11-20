using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Dtos;
using StargateAPI.Business.Helpers;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Queries
{
    public class GetPersonByName : IRequest<GetPersonByNameResult>
    {
        public required string Name { get; set; } = string.Empty;
    }

    public class GetPersonByNameHandler : IRequestHandler<GetPersonByName, GetPersonByNameResult>
    {
        private readonly StargateContext _context;
        public GetPersonByNameHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<GetPersonByNameResult> Handle(GetPersonByName request, CancellationToken cancellationToken)
        {
            var name = request.Name.TrimAndToUpperString();
            
            var personAstronaut = await _context.People
                .Include(x => x.AstronautDetail)
                .Select(x => new PersonAstronaut
                {
                    PersonId = x.Id,
                    Name = x.Name,
                    CurrentRank = x.AstronautDetail != null ? x.AstronautDetail.CurrentRank : null,
                    CurrentDutyTitle = x.AstronautDetail != null ? x.AstronautDetail.CurrentDutyTitle : null,
                    CareerStartDate = x.AstronautDetail != null ? x.AstronautDetail.CareerStartDate : null,
                    CareerEndDate = x.AstronautDetail != null ? x.AstronautDetail.CareerEndDate : null,
                })
                .SingleOrDefaultAsync(x => x.Name.Trim().ToUpper() == name, cancellationToken: cancellationToken);

            return new GetPersonByNameResult{ Person = personAstronaut };
        }
    }

    public class GetPersonByNameResult : BaseResponse
    {
        public PersonAstronaut? Person { get; set; }
    }
}
