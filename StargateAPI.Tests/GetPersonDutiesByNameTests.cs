using Microsoft.AspNetCore.Http;
using StargateAPI.Business.Data;
using StargateAPI.Business.Queries;

namespace StargateAPI.Tests;

public class GetPersonDutiesByNameTests
{
    private static async Task<GetAstronautDutiesByNameResult> Run(MockStargateContext db, string name)
    {
        var handler = new GetAstronautDutiesByNameHandler(db.Object);
        return await handler.Handle(new GetPersonDutiesByName { Name = name }, CancellationToken.None);
    }
    
    private static MockStargateContext JohnWithDutyHistory()
    {
        var john = new Person { Id = 1, Name = "John Doe" };

        var cadet = new AstronautDuty { Id = 1, PersonId = 1, Person = john, Rank = "2LT", DutyTitle = "CADET", DutyStartDate = new DateTime(2018, 1, 1), DutyEndDate = new DateTime(2019, 12, 31) };
        var commander = new AstronautDuty { Id = 3, PersonId = 1, Person = john, Rank = "CPT", DutyTitle = "COMMANDER", DutyStartDate = new DateTime(2024, 6, 1), DutyEndDate = null };
        var pilot = new AstronautDuty { Id = 2, PersonId = 1, Person = john, Rank = "1LT", DutyTitle = "PILOT", DutyStartDate = new DateTime(2020, 1, 1), DutyEndDate = new DateTime(2024, 5, 31) };

        john.AstronautDuties = new List<AstronautDuty> { cadet, commander, pilot };
        john.AstronautDetail = new AstronautDetail
        {
            PersonId = 1,
            Person = john,
            CurrentRank = "CPT",
            CurrentDutyTitle = "COMMANDER",
            CareerStartDate = new DateTime(2018, 1, 1)
        };

        return new MockStargateContext(
            people: [john],
            astronautDetails: [john.AstronautDetail],
            astronautDuties: [.. john.AstronautDuties]);
    }

    [Fact]
    public async Task Handler_UnknownName_Throws()
    {
        var db = JohnWithDutyHistory();

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(() => Run(db, "Nobody"));

        Assert.Equal("There's no astronaut with that name. Please try another name.", ex.Message);
    }

    [Fact]
    public async Task Handler_ReturnsAllDutiesNewestFirst()
    {
        var db = JohnWithDutyHistory();

        var result = await Run(db, "John Doe");

        Assert.True(result.Success);
        Assert.Equal(new[] { "COMMANDER", "PILOT", "CADET" }, result.AstronautDuties.Select(d => d.DutyTitle));
    }

    [Fact]
    public async Task Handler_Rule3_And_Rule4_CurrentDutyIsFirstAndOnlyOneWithoutEndDate()
    {
        var db = JohnWithDutyHistory();

        var result = await Run(db, "John Doe");

        var current = Assert.Single(result.AstronautDuties, d => d.DutyEndDate == null);
        Assert.Same(current, result.AstronautDuties.First());
    }

    [Fact]
    public async Task Handler_MapsCurrentAstronautInfoFromDetail()
    {
        var db = JohnWithDutyHistory();

        var result = await Run(db, "John Doe");

        Assert.Equal(1, result.Person.PersonId);
        Assert.Equal("John Doe", result.Person.Name);
        Assert.Equal("CPT", result.Person.CurrentRank);
        Assert.Equal("COMMANDER", result.Person.CurrentDutyTitle);
        Assert.Equal(new DateTime(2018, 1, 1), result.Person.CareerStartDate);
        Assert.Null(result.Person.CareerEndDate);
    }

    [Fact]
    public async Task Handler_Rule2_PersonWithoutAssignment_ReturnsNoDutiesAndNoAstronautInfo()
    {
        var db = new MockStargateContext(people: [new Person { Id = 1, Name = "Jane Doe" }]);

        var result = await Run(db, "Jane Doe");

        Assert.Equal("Jane Doe", result.Person.Name);
        Assert.Empty(result.AstronautDuties);
        Assert.Null(result.Person.CurrentRank);
        Assert.Null(result.Person.CurrentDutyTitle);
        Assert.Null(result.Person.CareerStartDate);
        Assert.Null(result.Person.CareerEndDate);
    }

    [Fact]
    public async Task Handler_ReturnsOnlyTheRequestedPersonsDuties()
    {
        var db = JohnWithDutyHistory();
        var jane = new Person { Id = 2, Name = "Jane Doe" };
        var janeDuty = new AstronautDuty { Id = 10, PersonId = 2, Person = jane, Rank = "1LT", DutyTitle = "ENGINEER", DutyStartDate = new DateTime(2023, 1, 1) };
        jane.AstronautDuties = new List<AstronautDuty> { janeDuty };
        db.People.Add(jane);
        db.AstronautDuties.Add(janeDuty);

        var result = await Run(db, "Jane Doe");

        var duty = Assert.Single(result.AstronautDuties);
        Assert.Equal("ENGINEER", duty.DutyTitle);
    }
}
