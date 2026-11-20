using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Commands
{
    public class CreatePerson : IRequest<CreatePersonResult>
    {
        public required string Name { get; set; } = string.Empty;
    }

    public class CreatePersonPreProcessor : IRequestPreProcessor<CreatePerson>
    {
        private readonly StargateContext _context;
        public CreatePersonPreProcessor(StargateContext context)
        {
            _context = context;
        }
        public Task Process(CreatePerson request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadHttpRequestException("Please enter a valid name.");
            }
            
            var doesPersonExist = _context.People.Any(x => x.Name.Trim().ToLower() == request.Name.Trim().ToLower());
            //Rule #1: A Person is uniquely identified by their Name.
            //Also made Name on the Person table Unique in the database. See Configure() on Person Model.
            if (doesPersonExist)
            {
                throw new BadHttpRequestException("Someone with this name already exists.");
            }

            return Task.CompletedTask;
        }
    }

    public class CreatePersonHandler : IRequestHandler<CreatePerson, CreatePersonResult>
    {
        private readonly StargateContext _context;

        public CreatePersonHandler(StargateContext context)
        {
            _context = context;
        }
        public async Task<CreatePersonResult> Handle(CreatePerson request, CancellationToken cancellationToken)
        {
            var newPerson = new Person()
            {
               Name = request.Name
            };
            
            await _context.People.AddAsync(newPerson, cancellationToken);
            
            await _context.SaveChangesAsync(cancellationToken);
            
            return new CreatePersonResult()
            {
                Id = newPerson.Id
            };
        }
    }

    public class CreatePersonResult : BaseResponse
    {
        public int Id { get; set; }
    }
}
