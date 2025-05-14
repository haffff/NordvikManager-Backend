using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Domain.Enums
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
        None
    }
}
