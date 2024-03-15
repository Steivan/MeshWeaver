﻿namespace OpenSmc.Charting.Models.Options;

public record DataLabels
{
    /// <summary>
    /// Defines the position of the label relative to the anchor point position and orientation. Default 'center'
    /// </summary>
    public object Align { get; set; }

    /// <summary>
    /// An anchor point is defined by an orientation vector and a position on the data element (center, start, end). Default 'center'
    /// </summary>
    public string Anchor { get; set; }

    public string Color { get; set; }

    public object Display { get; set; }
    
    public Font Font { get; set; }

    /// <summary>
    /// Data values formatter, function(value, context)
    /// </summary>
    public object Formatter { get; set; }

    public string TextAlign { get; set; }
}