﻿using Serenity.Web;

namespace Serenity.Services;

[Obsolete("Use Serenity.Services.MultipleFileUploadBehavior")]
public class MultipleImageUploadBehavior : MultipleFileUploadBehavior
{
    public MultipleImageUploadBehavior(ITextLocalizer localizer, IUploadStorage storage, IExceptionLogger logger = null)
        : base(storage, localizer, logger)
    {
    }
}