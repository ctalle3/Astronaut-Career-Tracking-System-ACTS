using Microsoft.AspNetCore.Http;
using Moq;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests;

public class CreatePersonTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task PreProcessor_EmptyOrWhitespaceName_Throws(string name)
    {
        var db = new MockStargateContext();
        var preProcessor = new CreatePersonPreProcessor(db.Object);

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
            () => preProcessor.Process(new CreatePerson { Name = name }, CancellationToken.None));

        Assert.Equal("Please enter a valid name.", ex.Message);
    }

    [Fact]
    public async Task PreProcessor_NewName_DoesNotThrow()
    {
        var db = new MockStargateContext(people: [new Person { Id = 1, Name = "John Doe" }]);
        var preProcessor = new CreatePersonPreProcessor(db.Object);

        var ex = await Record.ExceptionAsync(
            () => preProcessor.Process(new CreatePerson { Name = "Jane Doe" }, CancellationToken.None));

        Assert.Null(ex);
    }

    //Rule #1: A Person is uniquely identified by their Name
    [Theory]
    [InlineData("John Doe")]     // exact match
    [InlineData("john doe")]     // different case
    [InlineData("JOHN DOE")]     // different case
    [InlineData("  John Doe  ")] // surrounding whitespace
    public async Task PreProcessor_Rule1_ExistingName_Throws(string name)
    {
        var db = new MockStargateContext(people: [new Person { Id = 1, Name = "John Doe" }]);
        var preProcessor = new CreatePersonPreProcessor(db.Object);

        var ex = await Assert.ThrowsAsync<BadHttpRequestException>(
            () => preProcessor.Process(new CreatePerson { Name = name }, CancellationToken.None));

        Assert.Equal("Someone with this name already exists.", ex.Message);
    }
    
    [Fact]
    public async Task Handler_ValidName_AddsPersonAndSaves()
    {
        var db = new MockStargateContext();
        var handler = new CreatePersonHandler(db.Object);

        var result = await handler.Handle(new CreatePerson { Name = "Jane Doe" }, CancellationToken.None);

        var saved = Assert.Single(db.People);
        Assert.Equal("Jane Doe", saved.Name);
        Assert.True(result.Success);
        db.Context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handler_ReturnsIdOfNewPerson()
    {
        var db = new MockStargateContext();
        db.Context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => db.People.Single().Id = 42)
            .ReturnsAsync(1);
        var handler = new CreatePersonHandler(db.Object);

        var result = await handler.Handle(new CreatePerson { Name = "Jane Doe" }, CancellationToken.None);

        Assert.Equal(42, result.Id);
    }

    [Fact]
    public async Task Handler_PassesCancellationTokenThrough()
    {
        var db = new MockStargateContext();
        var handler = new CreatePersonHandler(db.Object);
        using var cts = new CancellationTokenSource();

        await handler.Handle(new CreatePerson { Name = "Jane Doe" }, cts.Token);

        db.PeopleSet.Verify(s => s.AddAsync(It.IsAny<Person>(), cts.Token), Times.Once);
        db.Context.Verify(c => c.SaveChangesAsync(cts.Token), Times.Once);
    }

    //Rule #2: A Person who has not had an astronaut assignment will not have Astronaut records
    [Fact]
    public async Task Handler_Rule2_CreatingPerson_DoesNotCreateAstronautRecords()
    {
        var db = new MockStargateContext();
        var handler = new CreatePersonHandler(db.Object);

        await handler.Handle(new CreatePerson { Name = "Jane Doe" }, CancellationToken.None);

        Assert.Empty(db.AstronautDetails);
        Assert.Empty(db.AstronautDuties);
        db.AstronautDetailsSet.Verify(s => s.AddAsync(It.IsAny<AstronautDetail>(), It.IsAny<CancellationToken>()), Times.Never);
        db.AstronautDutiesSet.Verify(s => s.AddAsync(It.IsAny<AstronautDuty>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
