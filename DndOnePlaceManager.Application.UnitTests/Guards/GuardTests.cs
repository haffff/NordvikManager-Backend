using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;

namespace DndOnePlaceManager.Application.UnitTests.Guards
{
    public class GuardTests
    {
        private class Dummy { }

        [Fact]
        public void NotFound_ThrowsResourceNotFoundException_WhenValueIsNull()
        {
            Dummy value = null;

            var ex = Assert.Throws<ResourceNotFoundException>(
                () => Guard.NotFound(value, "Dummy", "abc-123"));

            Assert.Contains("Dummy", ex.Message);
            Assert.Contains("abc-123", ex.Message);
        }

        [Fact]
        public void NotFound_DoesNotThrow_WhenValueIsNotNull()
        {
            var value = new Dummy();

            Guard.NotFound(value, "Dummy", "abc-123");
        }

        [Fact]
        public void Argument_SingleField_ThrowsWrongArgumentsException_WhenConditionFalse()
        {
            var ex = Assert.Throws<WrongArgumentsException>(
                () => Guard.Argument(false, "Name"));

            Assert.Contains("Name", ex.Message);
        }

        [Fact]
        public void Argument_SingleField_DoesNotThrow_WhenConditionTrue()
        {
            Guard.Argument(true, "Name");
        }

        [Fact]
        public void Argument_MultipleFields_ThrowsWrongArgumentsException_WhenConditionFalse()
        {
            var ex = Assert.Throws<WrongArgumentsException>(
                () => Guard.Argument(false, "Name", "Type"));

            Assert.Contains("Name", ex.Message);
            Assert.Contains("Type", ex.Message);
        }

        [Fact]
        public void Argument_MultipleFields_DoesNotThrow_WhenConditionTrue()
        {
            Guard.Argument(true, "Name", "Type");
        }
    }
}
