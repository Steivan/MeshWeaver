﻿using System.Collections.Immutable;
using Markdig.Parsers;
using Markdig.Syntax;

namespace OpenSmc.Layout.Markdown;

public class LayoutAreaComponentInfo(string area, BlockParser blockParser)
    : ContainerBlock(blockParser)
{
    public ImmutableDictionary<string, object> Options { get; set; } = ImmutableDictionary<string, object>.Empty;

    public string Area => area;
    public string DivId { get; set; } = Guid.NewGuid().ToString();

    public string Layout { get; set; }
    public object Address { get; set; }
    public object Id { get; set; }

    public LayoutAreaReference Reference =>
        new (Area) { Id = Id, Options = Options, Layout = Layout };
}

public record SourceInfo(string Type, string Reference, string Address);

