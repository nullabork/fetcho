namespace Fetcho.Common.Entities
{
    public enum WorkspaceResultTransformAction
    {
        None,
        DeleteAll,
        DeleteSpecificResults,
        DeleteByQueryText,

        CopyAllTo,
        CopySpecificTo,
        CopyByQueryText,

        MoveAllTo,
        MoveSpecificTo,
        MoveByQueryText,

        TagAll,
        TagSpecificResults,
        TagByQueryText,

        UntagAll,
        UntagSpecificResults,
        UntagByQueryText
    }
}
