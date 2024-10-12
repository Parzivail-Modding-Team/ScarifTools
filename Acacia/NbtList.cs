namespace Acacia;

public record NbtList(TagType ElementType, List<NbtElement> Elements) : NbtElement;