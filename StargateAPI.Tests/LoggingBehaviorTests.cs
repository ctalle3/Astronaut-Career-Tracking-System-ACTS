using MediatR;
using Moq;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Logging;

namespace StargateAPI.Tests;

public class LoggingBehaviorTests
{
    private static CreatePerson Request() => new() { Name = "John Doe" };

    [Fact]
    public async Task Handle_Success_LogsSuccessAndReturnsResponse()
    {
        var logger = new Mock<IProcessLogger>();
        var behavior = new LoggingBehavior<CreatePerson, CreatePersonResult>(logger.Object);
        var expected = new CreatePersonResult { Id = 7 };

        var result = await behavior.Handle(Request(), () => Task.FromResult(expected), CancellationToken.None);

        Assert.Same(expected, result);
        logger.Verify(l => l.LogSuccessAsync(
            "CreatePerson",
            It.Is<string>(d => d.Contains("John Doe")),
            "CreatePerson completed successfully."), Times.Once);
        logger.Verify(l => l.LogErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Exception_LogsErrorAndRethrows()
    {
        var logger = new Mock<IProcessLogger>();
        var behavior = new LoggingBehavior<CreatePerson, CreatePersonResult>(logger.Object);
        var thrown = new InvalidOperationException("boom");

        RequestHandlerDelegate<CreatePersonResult> next = () => throw thrown;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(Request(), next, CancellationToken.None));

        Assert.Same(thrown, ex);
        logger.Verify(l => l.LogErrorAsync(
            "CreatePerson",
            It.Is<string>(d => d.Contains("John Doe")),
            thrown), Times.Once);
        logger.Verify(l => l.LogSuccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
