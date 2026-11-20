using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Dtos;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Queries
{
    public class GetAllPeople : IRequest<GetPeopleResult>
    {

    }

    public class GetPeopleHandler : IRequestHandler<GetAllPeople, GetPeopleResult>
    {
        public readonly StargateContext _context;
        public GetPeopleHandler(StargateContext context)
        {
            _context = context;
        }
        
        public async Task<GetPeopleResult> Handle(GetAllPeople request, CancellationToken cancellationToken)
        {
            var personAstronauts = await _context.People
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
                .ToListAsync(cancellationToken: cancellationToken);

            if (personAstronauts == null) throw new BadHttpRequestException("There's no astronaut with that name. Please try another name.");

            return new GetPeopleResult{ People = personAstronauts };
        }
    }

    public class GetPeopleResult : BaseResponse
    {
        public List<PersonAstronaut> People { get; set; } = new List<PersonAstronaut> { };
    }
}
