using IngestionApi.Application.Common;

namespace IngestionApi.Tests.Unit.Application;

public class ResultTests
{
    [Fact]
    public void Success_HasIsSuccessTrue()
    {
        var r = Result.Success();
        Assert.True(r.IsSuccess);
        Assert.False(r.IsConflict);
        Assert.Null(r.Error);
    }

    [Fact]
    public void Failure_CarriesError()
    {
        var r = Result.Failure("boom");
        Assert.False(r.IsSuccess);
        Assert.False(r.IsConflict);
        Assert.Equal("boom", r.Error);
    }

    [Fact]
    public void ValidationFailure_DefaultMessage()
    {
        var r = Result.ValidationFailure();
        Assert.False(r.IsSuccess);
        Assert.Equal("Validation failed", r.Error);
    }

    [Fact]
    public void ValidationFailure_CustomMessage()
    {
        var r = Result.ValidationFailure("bad");
        Assert.Equal("bad", r.Error);
    }

    [Fact]
    public void Conflict_HasIsConflictTrue()
    {
        var r = Result.Conflict();
        Assert.False(r.IsSuccess);
        Assert.True(r.IsConflict);
        Assert.Equal("A conflict occurred with existing data.", r.Error);
    }

    [Fact]
    public void GenericSuccess_CarriesValue()
    {
        var r = Result.Success(42);
        Assert.True(r.IsSuccess);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void GenericFailure_NoValue()
    {
        var r = Result.Failure<int>("err");
        Assert.False(r.IsSuccess);
        Assert.Equal("err", r.Error);
        Assert.Equal(0, r.Value);
    }

    [Fact]
    public void GenericConflict_NoValue()
    {
        var r = Result.Conflict<string>();
        Assert.True(r.IsConflict);
        Assert.Null(r.Value);
    }
}

public class ConflictExceptionTests
{
    [Fact]
    public void DefaultCtor_HasMessage()
    {
        Assert.Equal("A conflict occurred with existing data.", new ConflictException().Message);
    }

    [Fact]
    public void MessageCtor_PreservesMessage()
    {
        Assert.Equal("x", new ConflictException("x").Message);
    }

    [Fact]
    public void InnerCtor_PreservesInner()
    {
        var inner = new InvalidOperationException("i");
        var ex = new ConflictException("x", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("x", ex.Message);
    }
}
