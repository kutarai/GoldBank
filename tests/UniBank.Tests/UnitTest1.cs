namespace UniBank.Tests;

using UniBank.SharedKernel.Results;

public class SolutionBuildVerificationTests
{
    [Fact]
    public void Solution_SharedKernel_Result_Success_IsSuccess()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Solution_SharedKernel_Result_Failure_IsFailure()
    {
        var result = Result.Failure(Error.NotFound);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal("Error.NotFound", result.Error.Code);
    }

    [Fact]
    public void Solution_SharedKernel_Result_Generic_Success_HasValue()
    {
        var result = Result.Success("test-value");

        Assert.True(result.IsSuccess);
        Assert.Equal("test-value", result.Value);
    }

    [Fact]
    public void Solution_SharedKernel_StatusCodes_HasExpectedValues()
    {
        Assert.Equal("00", SharedKernel.Constants.StatusCodes.Success);
        Assert.Equal("51", SharedKernel.Constants.StatusCodes.InsufficientFunds);
        Assert.Equal("96", SharedKernel.Constants.StatusCodes.SystemMalfunction);
    }
}
