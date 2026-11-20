using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests;

public class MockStargateContext
{
    public List<Person> People { get; }
    public List<AstronautDetail> AstronautDetails { get; }
    public List<AstronautDuty> AstronautDuties { get; }

    public Mock<StargateContext> Context { get; }
    public Mock<DbSet<Person>> PeopleSet { get; }
    public Mock<DbSet<AstronautDetail>> AstronautDetailsSet { get; }
    public Mock<DbSet<AstronautDuty>> AstronautDutiesSet { get; }

    public MockStargateContext(
        List<Person>? people = null,
        List<AstronautDetail>? astronautDetails = null,
        List<AstronautDuty>? astronautDuties = null)
    {
        People = people ?? new List<Person>();
        AstronautDetails = astronautDetails ?? new List<AstronautDetail>();
        AstronautDuties = astronautDuties ?? new List<AstronautDuty>();

        PeopleSet = BuildSet(People);
        AstronautDetailsSet = BuildSet(AstronautDetails);
        AstronautDutiesSet = BuildSet(AstronautDuties);

        Context = new Mock<StargateContext>(new DbContextOptions<StargateContext>());
        Context.Setup(c => c.People).Returns(PeopleSet.Object);
        Context.Setup(c => c.AstronautDetails).Returns(AstronautDetailsSet.Object);
        Context.Setup(c => c.AstronautDuties).Returns(AstronautDutiesSet.Object);
        Context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    public StargateContext Object => Context.Object;

    private static Mock<DbSet<T>> BuildSet<T>(List<T> data) where T : class
    {
        var set = data.BuildMockDbSet();

        set.Setup(s => s.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Callback<T, CancellationToken>((entity, _) => data.Add(entity))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T>>());

        return set;
    }
}
