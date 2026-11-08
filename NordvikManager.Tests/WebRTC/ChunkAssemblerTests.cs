using NordvikManager.WebRTC;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // Index and total come straight from the player's client, so what they can make the
    // backend allocate and hold has to be bounded.
    public class ChunkAssemblerTests
    {
        [Fact]
        public void Add_ReturnsWholeMessage_WhenLastChunkArrives()
        {
            var assembler = new ChunkAssembler();

            Assert.Equal(ChunkAssembler.Outcome.Waiting, assembler.Add("a", 1, 2, "world", out _));
            Assert.Equal(ChunkAssembler.Outcome.Complete, assembler.Add("a", 0, 2, "hello ", out var message));

            Assert.Equal("hello world", message);
        }

        [Fact]
        public void Add_CompletesOnce_WhenAChunkIsRepeated()
        {
            var assembler = new ChunkAssembler();

            assembler.Add("a", 0, 2, "x", out _);

            Assert.Equal(ChunkAssembler.Outcome.Waiting, assembler.Add("a", 0, 2, "y", out _));
            Assert.Equal(ChunkAssembler.Outcome.Complete, assembler.Add("a", 1, 2, "z", out var message));
            Assert.Equal("yz", message);
        }

        [Fact]
        public void Add_AcceptsTheLargestAllowedMessage_AtTheChunkLimit()
        {
            var assembler = new ChunkAssembler();

            Assert.Equal(ChunkAssembler.Outcome.Waiting, assembler.Add("a", 0, ChunkAssembler.DefaultMaxChunks, "x", out _));
        }

        [Theory]
        [InlineData(ChunkAssembler.DefaultMaxChunks + 1)]
        [InlineData(500_000_000)]
        [InlineData(int.MaxValue)]
        [InlineData(0)]
        [InlineData(-1)]
        public void Add_Rejects_WhenTotalIsOutOfRange(int total)
        {
            var assembler = new ChunkAssembler();

            Assert.Equal(ChunkAssembler.Outcome.Rejected, assembler.Add("a", 0, total, "x", out var message));
            Assert.Null(message);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(2)]
        [InlineData(int.MaxValue)]
        public void Add_Rejects_WhenIndexIsOutsideTotal(int index)
        {
            var assembler = new ChunkAssembler();

            Assert.Equal(ChunkAssembler.Outcome.Rejected, assembler.Add("a", index, 2, "x", out _));
        }

        [Fact]
        public void Add_RejectsAndForgetsTheMessage_WhenTotalChangesMidway()
        {
            var assembler = new ChunkAssembler();
            assembler.Add("a", 0, 3, "x", out _);

            Assert.Equal(ChunkAssembler.Outcome.Rejected, assembler.Add("a", 1, 2, "y", out _));

            // Forgotten: the id starts over and needs all of its chunks again.
            Assert.Equal(ChunkAssembler.Outcome.Waiting, assembler.Add("a", 1, 2, "y", out _));
            Assert.Equal(ChunkAssembler.Outcome.Complete, assembler.Add("a", 0, 2, "x", out var message));
            Assert.Equal("xy", message);
        }

        [Fact]
        public void Add_RejectsNewMessages_WhenTooManyAreUnfinished()
        {
            var assembler = new ChunkAssembler(maxPendingMessages: 2);
            assembler.Add("a", 0, 2, "x", out _);
            assembler.Add("b", 0, 2, "x", out _);

            Assert.Equal(ChunkAssembler.Outcome.Rejected, assembler.Add("c", 0, 2, "x", out _));
            Assert.Equal(ChunkAssembler.Outcome.Complete, assembler.Add("a", 1, 2, "y", out _));
            Assert.Equal(ChunkAssembler.Outcome.Waiting, assembler.Add("c", 0, 2, "x", out _));
        }

        [Fact]
        public void Add_RejectsAndFreesTheMessage_WhenHeldCharactersPassTheLimit()
        {
            var assembler = new ChunkAssembler(maxPendingChars: 10);
            assembler.Add("a", 0, 3, "12345678", out _);

            Assert.Equal(ChunkAssembler.Outcome.Rejected, assembler.Add("a", 1, 3, "123", out _));

            // Its characters no longer count.
            Assert.Equal(ChunkAssembler.Outcome.Waiting, assembler.Add("b", 0, 2, "12345678", out _));
        }

        [Fact]
        public void Add_CountsOnlyTheLatestCopy_WhenAChunkIsRepeated()
        {
            var assembler = new ChunkAssembler(maxPendingChars: 10);

            for (var i = 0; i < 5; i++)
                Assert.Equal(ChunkAssembler.Outcome.Waiting, assembler.Add("a", 0, 2, "12345678", out _));
        }

        [Fact]
        public void Add_FreesTheCharacters_WhenAMessageCompletes()
        {
            var assembler = new ChunkAssembler(maxPendingChars: 10);
            assembler.Add("a", 0, 2, "12345", out _);
            assembler.Add("a", 1, 2, "12345", out _);

            Assert.Equal(ChunkAssembler.Outcome.Waiting, assembler.Add("b", 0, 2, "1234567890", out _));
        }
    }
}
