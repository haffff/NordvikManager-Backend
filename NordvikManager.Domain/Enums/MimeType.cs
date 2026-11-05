using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NordvikManager.Domain.Enums
{
    public enum MimeType
    {
        [Description("image/png")]
        PNG,
        [Description("image/jpeg")]
        JPEG,
        [Description("image/gif")]
        GIF,
        [Description("image/tiff")]
        TIFF,
        [Description("audio/mpeg")]
        MP3,
        [Description("audio/wav")]
        Wave,
        [Description("audio/ogg")]
        Ogg,
        [Description("text/html")]
        HTML,
        [Description("text/css")]
        CSS,
        [Description("application/json")]
        JSON,
        [Description("text/javascript")]
        JavaScript,
        [Description("text/plain")]
        PlainText,
        [Description("image/webp")]
        WEBP,
        [Description("video/mp4")]
        MP4,
        [Description("video/webm")]
        WebM,
        None,
        // Stored as an int: new types go after None so existing resources keep theirs.
        [Description("application/pdf")]
        PDF,
    }
}
