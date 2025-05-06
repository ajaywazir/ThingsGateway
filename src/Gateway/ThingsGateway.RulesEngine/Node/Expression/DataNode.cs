﻿
using ThingsGateway.Gateway.Application.Extensions;

using TouchSocket.Core;

namespace ThingsGateway.RulesEngine;

[CategoryNode(Category = "Expression", ImgUrl = "_content/ThingsGateway.RulesEngine/img/CSharpScript.svg", Desc = nameof(DataNode), LocalizerType = typeof(ThingsGateway.RulesEngine._Imports), WidgetType = typeof(CSharpScriptWidget))]
public class DataNode : TextNode, IExpressionNode
{
    public DataNode(string id, Point? position = null) : base(id, position)
    {
        Title = "DataNode"; Placeholder = "DataNode.Placeholder";
        Text = "return 1;";

    }

    Task<NodeOutput> IExpressionNode.ExecuteAsync(NodeInput input, CancellationToken cancellationToken)
    {
        var value = Text.GetExpressionsResult(input.Value, Logger);
        NodeOutput nodeOutput = new();
        nodeOutput.Value = value;
        Logger?.Trace($"Data result: {nodeOutput.JToken?.ToString(Newtonsoft.Json.Formatting.Indented)}");
        return Task.FromResult(nodeOutput);
    }
}
