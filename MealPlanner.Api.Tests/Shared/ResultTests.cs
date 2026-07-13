using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Shared;

public class ResultTests
{
	private static readonly Error TestError = new(ErrorCodes.NotFound, "Not found");

	[Fact]
	public void Bind_InvokesFunc_WhenSuccessful()
	{
		var result = Result<int>.Success(2).Bind(v => Result<string>.Success($"value:{v}"));

		Assert.True(result.IsSuccess);
		Assert.Equal("value:2", result.Value);
	}

	[Fact]
	public void Bind_PropagatesError_WhenFailed()
	{
		var result = Result<int>.Failure(TestError).Bind(v => Result<string>.Success($"value:{v}"));

		Assert.False(result.IsSuccess);
		Assert.Same(TestError, result.Error);
	}

	[Fact]
	public void Bind_InvokesFunc_WhenSuccessValueIsNull()
	{
		var invoked = false;

		var result = Result<string?>.Success(null).Bind(_ =>
		{
			invoked = true;
			return Result<int>.Success(1);
		});

		Assert.True(invoked);
		Assert.True(result.IsSuccess);
	}

	[Fact]
	public void Map_TransformsValue_WhenSuccessful()
	{
		var result = Result<int>.Success(2).Map(v => v * 3);

		Assert.True(result.IsSuccess);
		Assert.Equal(6, result.Value);
	}

	[Fact]
	public void Map_PropagatesError_WhenFailed()
	{
		var result = Result<int>.Failure(TestError).Map(v => v * 3);

		Assert.False(result.IsSuccess);
		Assert.Same(TestError, result.Error);
	}

	[Fact]
	public void Map_TransformsValue_WhenSuccessValueIsNull()
	{
		var result = Result<string?>.Success(null).Map(v => v is null);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value);
	}

	[Fact]
	public void Match_InvokesOnSuccess_WhenSuccessful()
	{
		var outcome = Result<int>.Success(2).Match(v => $"ok:{v}", e => $"error:{e.Code}");

		Assert.Equal("ok:2", outcome);
	}

	[Fact]
	public void Match_InvokesOnFailure_WhenFailed()
	{
		var outcome = Result<int>.Failure(TestError).Match(v => $"ok:{v}", e => $"error:{e.Code}");

		Assert.Equal($"error:{ErrorCodes.NotFound}", outcome);
	}

	[Fact]
	public void Match_InvokesOnSuccess_WhenSuccessValueIsNull()
	{
		var outcome = Result<string?>.Success(null).Match(v => $"ok:{v ?? "null"}", e => $"error:{e.Code}");

		Assert.Equal("ok:null", outcome);
	}

	[Fact]
	public void ToUnit_ReturnsUnit_WhenSuccessful()
	{
		var result = Result<int>.Success(2).ToUnit();

		Assert.True(result.IsSuccess);
		Assert.Equal(Unit.Value, result.Value);
	}

	[Fact]
	public void ToUnit_PropagatesError_WhenFailed()
	{
		var result = Result<int>.Failure(TestError).ToUnit();

		Assert.False(result.IsSuccess);
		Assert.Same(TestError, result.Error);
	}
}
