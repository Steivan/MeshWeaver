﻿using OpenSmc.DataStructures;

namespace OpenSmc.DataSetReader;

/// <summary>
/// Implementations of the <see cref="ReadDataSet"/> read the data from a source and returns the data
/// in a <see cref="IDataSet"/> which then can be further processed in the import
/// </summary>
public delegate Task<(IDataSet DataSet, string Format)> ReadDataSet(Stream stream, DataSetReaderOptions options, CancellationToken cancellationToken);

public record DataSetReaderOptions 
{
    public  char Delimiter { get; init; } = ',';
    public  bool IncludeHeaderRow { get; init; } = true;
    public  Type TypeToRestoreHeadersFrom { get; init; }
    public string ContentType { get; init; }



    /// <summary>
    /// Defines delimiter for csv and strings of csv format
    /// </summary>
    public DataSetReaderOptions WithDelimiter(char delimiter)
    {
        return this with { Delimiter = delimiter };
    }
    /// <summary>
    /// Defines whether first table of csv contains header row, by default true
    /// </summary>
    public DataSetReaderOptions WithHeaderRow(bool withHeaderRow = true)
    {
        return this with { IncludeHeaderRow = withHeaderRow };
    }

    public DataSetReaderOptions WithContentType(string contentType)
    {
        return this with { ContentType = contentType };
    }

    public DataSetReaderOptions WithTypeToRestoreHeadersFrom(Type typeToRestoreHeadersFrom)
    {
        return this with { TypeToRestoreHeadersFrom = typeToRestoreHeadersFrom };
    }
}