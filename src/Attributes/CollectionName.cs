namespace Lib.MongoLite.Src.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CollectionName(string name) : Attribute
    {
        public string Name { get; } = name;
    }
}