﻿using PagedRequestBuilder.Common.ValueParser.Models;
using System;
using System.Text.Json.Nodes;

namespace PagedRequestBuilder.Common.ValueParser.Strategies;

public interface IValueParseStrategy
{
    ValueParseResult GetValue(JsonValue value, Type assignablePropertyType);
}