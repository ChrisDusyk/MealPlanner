using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.TestUtilities;

internal static class MongoTestHelpers
{
	internal static Mock<IAsyncCursor<T>> CreateCursor<T>(IReadOnlyCollection<T> items)
	{
		var cursor = new Mock<IAsyncCursor<T>>();
		cursor.SetupGet(c => c.Current).Returns(items);
		cursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
			.Returns(true)
			.Returns(false);
		cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(true)
			.ReturnsAsync(false);
		return cursor;
	}

	internal static Mock<IFindFluent<TDocument, TDocument>> CreateFindFluent<TDocument>(
		IReadOnlyCollection<TDocument> items)
	{
		var cursor = CreateCursor(items);
		var findFluent = new Mock<IFindFluent<TDocument, TDocument>>();
		findFluent.Setup(f => f.ToCursor(It.IsAny<CancellationToken>()))
			.Returns(cursor.Object);
		findFluent.Setup(f => f.ToCursorAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(cursor.Object);
		return findFluent;
	}
}
