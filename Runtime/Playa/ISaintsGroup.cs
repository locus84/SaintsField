﻿namespace SaintsField.Playa
{
    public interface ISaintsGroup
    {
        string GroupBy { get; }
        ELayout Layout { get; }
        bool KeepGrouping { get; }
    }
}
