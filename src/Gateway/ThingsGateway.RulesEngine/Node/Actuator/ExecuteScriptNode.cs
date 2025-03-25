
using ThingsGateway.Gateway.Application;
using ThingsGateway.NewLife;

using TouchSocket.Core;

namespace ThingsGateway.RulesEngine;

[CategoryNode(Category = "Actuator", ImgUrl = "_content/ThingsGateway.RulesEngine/img/CSharpScript.svg", Desc = nameof(ExecuteScriptNode), LocalizerType = typeof(ThingsGateway.RulesEngine._Imports), WidgetType = typeof(CSharpScriptWidget))]
public class ExecuteScriptNode : TextNode, IActuatorNode, IExexcuteExpressionsBase
{
    public ExecuteScriptNode(string id, Point? position = null) : base(id, position) { Title = "ExecuteScriptNode"; Placeholder = "ExecuteScriptNode.Placeholder"; }

    private string text;

    [ModelValue]
    public override string Text
    {
        get
        {
            return text;
        }
        set
        {
            if (text != value)
            {
                try
                {
                    var exexcuteExpressions = CSharpScriptEngineExtension.Do<IExexcuteExpressions>(text);
                    exexcuteExpressions.TryDispose();
                }
                catch
                {

                }
                CSharpScriptEngineExtension.Remove(text);
            }

            text = value;
        }
    }

    Task<NodeOutput> IActuatorNode.ExecuteAsync(NodeInput input, CancellationToken cancellationToken)
    {
        Logger?.Trace($"Execute script");
        var exexcuteExpressions = CSharpScriptEngineExtension.Do<IExexcuteExpressions>(Text);
        exexcuteExpressions.Logger = Logger;
        return exexcuteExpressions.ExecuteAsync(input, cancellationToken);

    }
}
