using Microsoft.AspNetCore.Http;
using Moq;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests;

public class CreateAstronautDutyTests
{
    private static readonly DateTime FirstDutyStart = new(2020, 1, 1);
    private static readonly DateTime NewDutyStart = new(2024, 6, 1);

    // ---------- Test data helpers ----------

    private static CreateAstronautDuty Request(
        string name = "John Doe",
        string rank = "1LT",
        string dutyTitle = "Pilot",
        DateTime? dutyStartDate = null) => new()
    {
        Name = name,
        Rank = rank,
        DutyTitle = dutyTitle,
        DutyStartDate = dutyStartDate ?? NewDutyStart
    };

    private static MockStargateContext PersonWithNoDuties()
    {
        return new MockStargateContext(people: [new Person { Id = 1, Name = "John Doe" }]);
    }

    private static MockStargateContext PersonWithCurrentDuty(string currentTitle = "PILOT", DateTime? careerEndDate = null)
    {
        var person = new Person { Id = 1, Name = "John Doe" };

        var detail = new AstronautDetail
        {
            Id = 1,
            PersonId = 1,
            Person = person,
            CurrentRank = "2LT",
            CurrentDutyTitle = currentTitle,
            CareerStartDate = FirstDutyStart,
            CareerEndDate = careerEndDate
        };

        var currentDuty = new AstronautDuty
        {
            Id = 1,
            PersonId = 1,
            Person = person,
            Rank = "2LT",
            DutyTitle = currentTitle,
            DutyStartDate = FirstDutyStart,
            DutyEndDate = null
        };

        return new MockStargateContext(
            people: [person],
            astronautDetails: [detail],
            astronautDuties: [currentDuty]);
    }

    private static void VerifyNothingSaved(MockStargateContext db)
    {
        db.Context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "1LT", "Pilot", "Please enter a valid name.")]
    [InlineData("  ", "1LT", "Pilot", "Please enter a valid name.")]
    [InlineData("John Doe", "", "Pilot", "Please enter a valid rank.")]
    [InlineData("John Doe", "  ", "Pilot", "Please enter a valid rank.")]
    [InlineData("John Doe", "1LT", "", "Please enter a valid duty title.")]
    [InlineData("John Doe", "1LT", "  ", "Please enter a valid duty title.")]
    public async Task PreProcessor_MissingRequiredField_Throws(string name, string rank, string title, string expectedMessage)
    {
        var db = PersonWithNoDuties();
        var preProcessor = new CreateAstronautDutyPreProcessor(db.Object);

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
            () => preProcessor.Process(Request(name, rank, title), CancellationToken.None));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public async Task PreProcessor_DefaultStartDate_Throws()
    {
        var db = PersonWithNoDuties();
        var preProcessor = new CreateAstronautDutyPreProcessor(db.Object);

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
            () => preProcessor.Process(Request(dutyStartDate: default(DateTime)), CancellationToken.None));

        Assert.Equal("Please enter a valid duty start date.", ex.Message);
    }

    [Fact]
    public async Task PreProcessor_ValidRequest_DoesNotThrow()
    {
        var db = PersonWithCurrentDuty();
        var preProcessor = new CreateAstronautDutyPreProcessor(db.Object);

        var ex = await Record.ExceptionAsync(
            () => preProcessor.Process(Request(), CancellationToken.None));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData("PILOT")]
    [InlineData("pilot")]
    [InlineData("  Pilot  ")]
    public async Task PreProcessor_SameDutyAlreadyExistsForPerson_Throws(string title)
    {
        var db = PersonWithCurrentDuty("PILOT");
        var preProcessor = new CreateAstronautDutyPreProcessor(db.Object);

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
            () => preProcessor.Process(Request(rank: "2LT", dutyTitle: title, dutyStartDate: FirstDutyStart.AddHours(10)), CancellationToken.None));

        Assert.Equal("This duty already exists for this user.", ex.Message);
    }

    [Fact]
    public async Task PreProcessor_SameTitleAndDateButDifferentRank_IsNotDuplicate()
    {
        var db = PersonWithCurrentDuty("PILOT");
        var preProcessor = new CreateAstronautDutyPreProcessor(db.Object);

        var ex = await Record.ExceptionAsync(
            () => preProcessor.Process(Request(rank: "1LT", dutyTitle: "PILOT", dutyStartDate: FirstDutyStart), CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task PreProcessor_SameDutyExistsForDifferentPerson_DoesNotThrow()
    {
        var db = PersonWithCurrentDuty("PILOT");
        db.People.Add(new Person { Id = 2, Name = "Jane Doe" });
        var preProcessor = new CreateAstronautDutyPreProcessor(db.Object);

        var ex = await Record.ExceptionAsync(
            () => preProcessor.Process(Request(name: "Jane Doe", dutyTitle: "Pilot", dutyStartDate: FirstDutyStart), CancellationToken.None));

        Assert.Null(ex);
    }

    // ---------- Rule #6 (assumption): an already-retired person can't be retired again ----------
    [Theory]
    [InlineData("RETIRED")]
    [InlineData("retired")]
    public async Task PreProcessor_Rule6_RetiredPersonRetiringAgain_Throws(string title)
    {
        var db = PersonWithCurrentDuty("RETIRED", careerEndDate: FirstDutyStart.AddDays(-1));
        var preProcessor = new CreateAstronautDutyPreProcessor(db.Object);

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
            () => preProcessor.Process(Request(dutyTitle: title), CancellationToken.None));

        Assert.Contains("already retired", ex.Message);
    }

    [Fact]
    public async Task PreProcessor_Rule6_RetiredPersonGettingNonRetiredDuty_DoesNotThrow()
    {
        var db = PersonWithCurrentDuty("RETIRED", careerEndDate: FirstDutyStart.AddDays(-1));
        var preProcessor = new CreateAstronautDutyPreProcessor(db.Object);

        var ex = await Record.ExceptionAsync(
            () => preProcessor.Process(Request(dutyTitle: "Pilot"), CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Handler_PersonNotFound_ThrowsAndSavesNothing()
    {
        var db = PersonWithNoDuties();
        var handler = new CreateAstronautDutyHandler(db.Object);

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
            () => handler.Handle(Request(name: "Nobody"), CancellationToken.None));

        Assert.Contains("There is no astronaut with this name", ex.Message);
        VerifyNothingSaved(db);
    }

    [Theory]
    [InlineData("john doe")]
    [InlineData("JOHN DOE")]
    [InlineData("  John Doe  ")]
    public async Task Handler_Rule1_FindsPersonIgnoringCaseAndWhitespace(string name)
    {
        var db = PersonWithNoDuties();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(name: name), CancellationToken.None);

        var duty = Assert.Single(db.AstronautDuties);
        Assert.Equal(1, duty.PersonId);
    }

    [Fact]
    public async Task Handler_Rule2_FirstDuty_CreatesAstronautDetail()
    {
        var db = PersonWithNoDuties();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(rank: "1LT", dutyTitle: "Pilot"), CancellationToken.None);

        var detail = Assert.Single(db.AstronautDetails);
        Assert.Equal(1, detail.PersonId);
        Assert.Equal("PILOT", detail.CurrentDutyTitle);
        Assert.Equal("1LT", detail.CurrentRank, ignoreCase: true);
        Assert.Equal(NewDutyStart, detail.CareerStartDate);
        Assert.Null(detail.CareerEndDate);
    }

    [Fact]
    public async Task Handler_Rule4_FirstDuty_HasNoEndDate()
    {
        var db = PersonWithNoDuties();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(), CancellationToken.None);

        var duty = Assert.Single(db.AstronautDuties);
        Assert.Null(duty.DutyEndDate);
        Assert.Equal(NewDutyStart, duty.DutyStartDate);
        Assert.Equal("PILOT", duty.DutyTitle);
    }

    [Fact]
    public async Task Handler_Rule7_FirstDutyRetired_ThrowsAndSavesNothing()
    {
        var db = PersonWithNoDuties();
        var handler = new CreateAstronautDutyHandler(db.Object);

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
            () => handler.Handle(Request(dutyTitle: "RETIRED"), CancellationToken.None));

        Assert.Contains("cannot be retired without a previous astronaut duty", ex.Message);
        Assert.Empty(db.AstronautDetails);
        Assert.Empty(db.AstronautDuties);
        VerifyNothingSaved(db);
    }

    [Fact]
    public async Task Handler_StoresStartDateWithoutTimeOfDay()
    {
        var db = PersonWithNoDuties();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(dutyStartDate: NewDutyStart.AddHours(15).AddMinutes(30)), CancellationToken.None);

        Assert.Equal(NewDutyStart, db.AstronautDuties.Single().DutyStartDate);
        Assert.Equal(NewDutyStart, db.AstronautDetails.Single().CareerStartDate);
    }

    [Fact]
    public async Task Handler_Rule5_PreviousDutyEndsDayBeforeNewDutyStarts()
    {
        var db = PersonWithCurrentDuty();
        var previousDuty = db.AstronautDuties.Single();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(dutyStartDate: NewDutyStart), CancellationToken.None);

        Assert.Equal(NewDutyStart.AddDays(-1), previousDuty.DutyEndDate);
    }

    [Fact]
    public async Task Handler_Rule3_And_Rule4_OnlyNewDutyIsCurrent()
    {
        var db = PersonWithCurrentDuty();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(dutyTitle: "Commander"), CancellationToken.None);

        Assert.Equal(2, db.AstronautDuties.Count);
        var current = Assert.Single(db.AstronautDuties, d => d.DutyEndDate == null);
        Assert.Equal("COMMANDER", current.DutyTitle);
        Assert.Equal(NewDutyStart, current.DutyStartDate);
    }

    [Fact]
    public async Task Handler_Rule3_UpdatesExistingDetailInsteadOfAddingAnother()
    {
        var db = PersonWithCurrentDuty();
        var detail = db.AstronautDetails.Single();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(rank: "CPT", dutyTitle: "Commander"), CancellationToken.None);

        Assert.Single(db.AstronautDetails);
        Assert.Equal("COMMANDER", detail.CurrentDutyTitle);
        Assert.Equal("CPT", detail.CurrentRank, ignoreCase: true);
        Assert.Equal(FirstDutyStart, detail.CareerStartDate); // career start doesn't move
        Assert.Null(detail.CareerEndDate);
        db.AstronautDetailsSet.Verify(s => s.AddAsync(It.IsAny<AstronautDetail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handler_PreviouslyEndedDuties_AreNotChanged()
    {
        var db = PersonWithCurrentDuty();
        var oldEnded = new AstronautDuty
        {
            Id = 2,
            PersonId = 1,
            Rank = "2LT",
            DutyTitle = "CADET",
            DutyStartDate = new DateTime(2018, 1, 1),
            DutyEndDate = new DateTime(2019, 12, 31)
        };
        db.AstronautDuties.Add(oldEnded);
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(), CancellationToken.None);

        Assert.Equal(new DateTime(2019, 12, 31), oldEnded.DutyEndDate);
    }

    [Theory]
    [InlineData(0)]   // same day as current duty
    [InlineData(-1)]  // day before current duty
    [InlineData(-365)]
    public async Task Handler_NewDutyNotAfterCurrentDuty_ThrowsAndSavesNothing(int daysFromCurrentStart)
    {
        var db = PersonWithCurrentDuty();
        var currentDuty = db.AstronautDuties.Single();
        var handler = new CreateAstronautDutyHandler(db.Object);

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
            () => handler.Handle(Request(dutyStartDate: FirstDutyStart.AddDays(daysFromCurrentStart)), CancellationToken.None));

        Assert.Equal("New duty must start after the current duty's start date.", ex.Message);
        Assert.Null(currentDuty.DutyEndDate);
        Assert.Single(db.AstronautDuties);
        VerifyNothingSaved(db);
    }

    [Theory]
    [InlineData("RETIRED")]
    [InlineData("retired")]
    [InlineData("  Retired ")]
    public async Task Handler_Rule6_And_Rule7_Retiring_SetsCareerEndDateToDayBefore(string title)
    {
        var db = PersonWithCurrentDuty();
        var detail = db.AstronautDetails.Single();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(dutyTitle: title, dutyStartDate: NewDutyStart), CancellationToken.None);

        Assert.Equal("RETIRED", detail.CurrentDutyTitle);
        Assert.Equal(NewDutyStart.AddDays(-1), detail.CareerEndDate);
        Assert.Equal("RETIRED", db.AstronautDuties.Single(d => d.DutyEndDate == null).DutyTitle);
    }

    [Fact]
    public async Task Handler_Rule6_Retiring_StillClosesPreviousDuty()
    {
        var db = PersonWithCurrentDuty();
        var previousDuty = db.AstronautDuties.Single();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(dutyTitle: "RETIRED", dutyStartDate: NewDutyStart), CancellationToken.None);

        Assert.Equal(NewDutyStart.AddDays(-1), previousDuty.DutyEndDate);
    }

    [Fact]
    public async Task Handler_Rule6_ComingOutOfRetirement_ClearsCareerEndDate()
    {
        // Assumption: a retired astronaut can return to duty
        var db = PersonWithCurrentDuty("RETIRED", careerEndDate: FirstDutyStart.AddDays(-1));
        var detail = db.AstronautDetails.Single();
        var handler = new CreateAstronautDutyHandler(db.Object);

        await handler.Handle(Request(dutyTitle: "Pilot"), CancellationToken.None);

        Assert.Equal("PILOT", detail.CurrentDutyTitle);
        Assert.Null(detail.CareerEndDate);
    }

    [Fact]
    public async Task Handler_SavesOnceAndReturnsNewDutyId()
    {
        var db = PersonWithCurrentDuty();
        db.Context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => db.AstronautDuties.Single(d => d.DutyEndDate == null).Id = 99)
            .ReturnsAsync(1);
        var handler = new CreateAstronautDutyHandler(db.Object);

        var result = await handler.Handle(Request(), CancellationToken.None);

        Assert.Equal(99, result.Id);
        Assert.True(result.Success);
        db.Context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handler_PassesCancellationTokenThrough()
    {
        var db = PersonWithNoDuties();
        var handler = new CreateAstronautDutyHandler(db.Object);
        using var cts = new CancellationTokenSource();

        await handler.Handle(Request(), cts.Token);

        db.AstronautDutiesSet.Verify(s => s.AddAsync(It.IsAny<AstronautDuty>(), cts.Token), Times.Once);
        db.Context.Verify(c => c.SaveChangesAsync(cts.Token), Times.Once);
    }
}