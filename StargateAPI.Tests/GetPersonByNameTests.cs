using StargateAPI.Business.Data;
using StargateAPI.Business.Queries;

namespace StargateAPI.Tests;

public class GetPersonByNameTests
{
    private static async Task<GetPersonByNameResult> Run(MockStargateContext db, string name)
    {
        var handler = new GetPersonByNameHandler(db.Object);
        return await handler.Handle(new GetPersonByName { Name = name }, CancellationToken.None);
    }

    private static Person Astronaut(int id, string name, string title)
    {
        var person = new Person { Id = id, Name = name };
        person.AstronautDetail = new AstronautDetail
        {
            PersonId = id,
            Person = person,
            CurrentRank = "1LT",
            CurrentDutyTitle = title,
            CareerStartDate = new DateTime(2020, 1, 1)
        };
        return person;
    }

    [Fact]
    public async Task Handler_ExistingAstronaut_ReturnsPersonWithAstronautInfo()
    {
        var john = Astronaut(1, "John Doe", "PILOT");
        var db = new MockStargateContext(people: [john], astronautDetails: [john.AstronautDetail!]);

        var result = await Run(db, "John Doe");

        Assert.True(result.Success);
        Assert.NotNull(result.Person);
        Assert.Equal(1, result.Person.PersonId);
        Assert.Equal("John Doe", result.Person.Name);
        Assert.Equal("1LT", result.Person.CurrentRank);
        Assert.Equal("PILOT", result.Person.CurrentDutyTitle);
        Assert.Equal(new DateTime(2020, 1, 1), result.Person.CareerStartDate);
        Assert.Null(result.Person.CareerEndDate);
    }

    [Fact]
    public async Task Handler_Rule2_PersonWithoutAssignment_ReturnsPersonWithNoAstronautInfo()
    {
        var db = new MockStargateContext(people: [new Person { Id = 1, Name = "Jane Doe" }]);

        var result = await Run(db, "Jane Doe");

        Assert.NotNull(result.Person);
        Assert.Equal("Jane Doe", result.Person.Name);
        Assert.Null(result.Person.CurrentRank);
        Assert.Null(result.Person.CurrentDutyTitle);
        Assert.Null(result.Person.CareerStartDate);
        Assert.Null(result.Person.CareerEndDate);
    }

    [Fact]
    public async Task Handler_Rule1_ReturnsOnlyTheMatchingPerson()
    {
        var john = Astronaut(1, "John Doe", "PILOT");
        var jane = Astronaut(2, "Jane Doe", "COMMANDER");
        var db = new MockStargateContext(people: [john, jane], astronautDetails: [john.AstronautDetail!, jane.AstronautDetail!]);

        var result = await Run(db, "Jane Doe");

        Assert.NotNull(result.Person);
        Assert.Equal(2, result.Person.PersonId);
        Assert.Equal("COMMANDER", result.Person.CurrentDutyTitle);
    }

    [Fact]
    public async Task Handler_UnknownName_ReturnsNullPerson()
    {
        var db = new MockStargateContext(people: [new Person { Id = 1, Name = "John Doe" }]);

        var result = await Run(db, "Nobody");

        Assert.Null(result.Person);
    }
}
