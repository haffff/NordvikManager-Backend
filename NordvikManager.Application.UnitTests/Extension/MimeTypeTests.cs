using NordvikManager.Application.Extension;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.UnitTests.Extension
{
    public class MimeTypeTests
    {
        // Resources store MimeType as an int column: a new type must go after None,
        // or every resource already in a database would change type.
        [Fact]
        public void MimeType_ExistingValues_KeepTheirNumbers()
        {
            Assert.Equal(0, (int)MimeType.PNG);
            Assert.Equal(14, (int)MimeType.WebM);
            Assert.Equal(15, (int)MimeType.None);
            Assert.Equal(16, (int)MimeType.PDF);
        }

        [Fact]
        public void ToMimeType_ReturnsPdf_ForPdfFile()
        {
            Assert.Equal(MimeType.PDF, "Players Handbook.PDF".ToMimeType());
        }

        [Fact]
        public void ToEnumUsingDescriptionAttribute_ReturnsPdf_ForApplicationPdf()
        {
            Assert.Equal(MimeType.PDF, "application/pdf".ToEnumUsingDescriptionAttribute<MimeType>());
            Assert.Equal("application/pdf", MimeType.PDF.GetDescriptionValue());
        }
    }
}
