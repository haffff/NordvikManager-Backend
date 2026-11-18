using NordvikManager.Services.Implementations;
using System.Linq;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations
{
    public class ActionCascadeTests
    {
        [Fact]
        public void TryEnter_StopsAtTheDepthLimit()
        {
            var cascade = ActionCascade.Start(maxDepth: 3, maxRuns: 100);

            for (var i = 0; i < 3; i++)
                cascade = cascade.TryEnter();

            Assert.NotNull(cascade);
            Assert.Null(cascade.TryEnter());
        }

        [Fact]
        public void Siblings_DoNotAddDepth()
        {
            var root = ActionCascade.Start(maxDepth: 1, maxRuns: 100);

            Assert.All(Enumerable.Range(0, 10), _ => Assert.NotNull(root.TryEnter()));
        }

        [Fact]
        public void TheRunBudget_IsSharedByTheWholeChain()
        {
            // Depth alone doesn't bound an action calling itself twice: that is 2^depth runs.
            var root = ActionCascade.Start(maxDepth: 10, maxRuns: 3);
            var child = root.TryEnter();

            Assert.NotNull(child.TryEnter());
            Assert.NotNull(root.TryEnter());
            Assert.Null(child.TryEnter());
            Assert.Null(root.TryEnter());
        }

        [Fact]
        public void ARefusal_IsReportedOnce_PerChain()
        {
            var root = ActionCascade.Start(maxDepth: 1, maxRuns: 100);
            var child = root.TryEnter();

            Assert.True(child.FirstRefusal());
            Assert.False(root.FirstRefusal());
            Assert.True(ActionCascade.Start().FirstRefusal());
        }
    }
}
