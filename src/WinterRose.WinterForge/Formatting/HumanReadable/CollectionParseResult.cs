namespace WinterRose.WinterForgeSerializing.Formatting
{
    public partial class HumanReadableParser
    {
        private enum CollectionParseResult
        {
            Failed,
            NotACollection,
            ListOrArray,
            Dictionary
        }
    }
}
