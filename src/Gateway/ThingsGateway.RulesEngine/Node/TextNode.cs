namespace ThingsGateway.RulesEngine;

public abstract class TextNode : PlaceholderModel
{

    public TextNode(string id, Point? position = null) : base(id, position)
    {
    }

    [ModelValue]
    public virtual string Text { get; set; }
}
