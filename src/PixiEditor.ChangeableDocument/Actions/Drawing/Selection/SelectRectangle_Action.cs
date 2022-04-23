﻿using ChunkyImageLib.DataHolders;
using PixiEditor.ChangeableDocument.Changes;
using PixiEditor.ChangeableDocument.Changes.Drawing;

namespace PixiEditor.ChangeableDocument.Actions.Drawing.Selection;

public record class SelectRectangle_Action : IStartOrUpdateChangeAction
{
    public Vector2i Pos { get; }
    public Vector2i Size { get; }
    public SelectRectangle_Action(Vector2i pos, Vector2i size)
    {
        Pos = pos;
        Size = size;
    }

    UpdateableChange IStartOrUpdateChangeAction.CreateCorrespondingChange()
    {
        return new SelectRectangle_UpdateableChange(Pos, Size);
    }

    void IStartOrUpdateChangeAction.UpdateCorrespodingChange(UpdateableChange change)
    {
        ((SelectRectangle_UpdateableChange)change).Update(Pos, Size);
    }
}
