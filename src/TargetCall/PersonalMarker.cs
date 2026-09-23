namespace TargetCall;

// A personal marker never enters TargetSync or any routed RPC.
internal sealed class PersonalMarker
{
    public long UserId { get; private set; }
    public uint ObjectId { get; private set; }
    public bool Active => UserId != 0 || ObjectId != 0;

    public bool Matches(long user, uint id) => Active && UserId == user && ObjectId == id;

    public bool Toggle(long user, uint id)
    {
        if (Matches(user, id)) { Clear(); return false; }
        UserId = user;
        ObjectId = id;
        return Active;
    }

    public void Clear()
    {
        UserId = 0;
        ObjectId = 0;
    }
}
