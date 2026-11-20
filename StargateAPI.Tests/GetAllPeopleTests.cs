using StargateAPI.Business.Data;
using StargateAPI.Business.Queries;

namespace StargateAPI.Tests;

public class GetAllPeopleTests
{
    private static async Task<GetPeopleResult> Run(MockStargateContext db)
    {
        var handler = new GetPeopleHandler(db.Object);
        return await handler.Handle(new GetAllPeople(), CancellationToken.None);
    }

    [Fact]
    public async Task Handler_NoPeople_ReturnsEmptyList()
    {
        var result = await Run(new MockStargateContext());

        Assert.True(result.Success);
        Assert.Empty(result.People);
    }

    [Fact]
    public async Task Handler_Rule2_PersonWithoutAssignment_HasNoAstronautInfo()
    {
        var db = new MockStargateContext(people: [new Person { Id = 1, Name = "Jane Doe" }]);

        var result = await Run(db);

        var person = Assert.Single(result.People);
        Assert.Equal(1, person.PersonId);
        Assert.Equal("Jane Doe", person.Name);
        Assert.Null(person.CurrentRank);
        Assert.Null(person.CurrentDutyTitle);
        Assert.Null(person.CareerStartDate);
        Assert.Null(person.CareerEndDate);
    }

    [Fact]
    public async Task Handler_PersonWithDetail_MapsCurrentAstronautInfo()
    {
        var person = new Person { Id = 1, Name = "John Doe" };
        person.AstronautDetail = new AstronautDetail
        {
            PersonId = 1,
            Person = person,
            CurrentRank = "CPT",
            CurrentDutyTitle = "RETIRED",
            CareerStartDate = new DateTime(2020, 1, 1),
            CareerEndDate = new DateTime(2024, 5, 31)
        };
        var db = new MockStargateContext(people: [person], astronautDetails: [person.AstronautDetail]);

        var result = await Run(db);

        var dto = Assert.Single(result.People);
        Assert.Equal("CPT", dto.CurrentRank);
        Assert.Equal("RETIRED", dto.CurrentDutyTitle);
        Assert.Equal(new DateTime(2020, 1, 1), dto.CareerStartDate);
        Assert.Equal(new DateTime(2024, 5, 31), dto.CareerEndDate);
    }

    [Fact]
    public async Task Handler_MixOfAstronautsAndNonAstronauts_ReturnsEveryone()
    {
        var astronaut = new Person { Id = 1, Name = "John Doe" };
        astronaut.AstronautDetail = new AstronautDetail
        {
            PersonId = 1,
            Person = astronaut,
            CurrentRank = "1LT",
            CurrentDutyTitle = "PILOT",
            CareerStartDate = new DateTime(2020, 1, 1)
        };
        var civilian = new Person { Id = 2, Name = "Jane Doe" };
        var db = new MockStargateContext(people: [astronaut, civilian], astronautDetails: [astronaut.AstronautDetail]);

        var result = await Run(db);

        Assert.Equal(2, result.People.Count);
        Assert.Equal("PILOT", result.People.Single(p => p.PersonId == 1).CurrentDutyTitle);
        Assert.Null(result.People.Single(p => p.PersonId == 2).CurrentDutyTitle);
    }
}
