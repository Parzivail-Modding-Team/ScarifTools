namespace Acacia;

public record NbtCompound(Dictionary<string, NbtElement> Elements) : NbtElement
{
	public NbtCompound ShallowCopy()
	{
		return new NbtCompound(new Dictionary<string, NbtElement>(Elements));
	}
	
	public bool TryGetByte(string name, out NbtByte? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtByte castValue)
			return false;

		value = castValue;
		return true;
	}

	public sbyte GetByte(string name)
	{
		if (!TryGetByte(name, out var value))
			throw new KeyNotFoundException($"No NbtByte element named {name}");

		return value.Value;
	}

	public bool TryGetShort(string name, out NbtShort? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtShort castValue)
			return false;

		value = castValue;
		return true;
	}

	public short GetShort(string name)
	{
		if (!TryGetShort(name, out var value))
			throw new KeyNotFoundException($"No NbtShort element named {name}");

		return value.Value;
	}

	public bool TryGetInt(string name, out NbtInt? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtInt castValue)
			return false;

		value = castValue;
		return true;
	}

	public int GetInt(string name)
	{
		if (!TryGetInt(name, out var value))
			throw new KeyNotFoundException($"No NbtInt element named {name}");

		return value.Value;
	}

	public bool TryGetLong(string name, out NbtLong? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtLong castValue)
			return false;

		value = castValue;
		return true;
	}

	public long GetLong(string name)
	{
		if (!TryGetLong(name, out var value))
			throw new KeyNotFoundException($"No NbtLong element named {name}");

		return value.Value;
	}

	public bool TryGetFloat(string name, out NbtFloat? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtFloat castValue)
			return false;

		value = castValue;
		return true;
	}

	public float GetFloat(string name)
	{
		if (!TryGetFloat(name, out var value))
			throw new KeyNotFoundException($"No NbtFloat element named {name}");

		return value.Value;
	}

	public bool TryGetDouble(string name, out NbtDouble? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtDouble castValue)
			return false;

		value = castValue;
		return true;
	}

	public double GetDouble(string name)
	{
		if (!TryGetDouble(name, out var value))
			throw new KeyNotFoundException($"No NbtDouble element named {name}");

		return value.Value;
	}

	public bool TryGetByteArray(string name, out NbtByteArray? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtByteArray castValue)
			return false;

		value = castValue;
		return true;
	}

	public sbyte[] GetByteArray(string name)
	{
		if (!TryGetByteArray(name, out var value))
			throw new KeyNotFoundException($"No NbtByteArray element named {name}");

		return value.Elements;
	}

	public bool TryGetString(string name, out NbtString? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtString castValue)
			return false;

		value = castValue;
		return true;
	}

	public string GetString(string name)
	{
		if (!TryGetString(name, out var value))
			throw new KeyNotFoundException($"No NbtString element named {name}");

		return value.Value;
	}

	public bool TryGetList(string name, out NbtList? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtList castValue)
			return false;

		value = castValue;
		return true;
	}

	public NbtList GetList(string name)
	{
		if (!TryGetList(name, out var value))
			throw new KeyNotFoundException($"No NbtList element named {name}");

		return value;
	}

	public bool TryGetCompound(string name, out NbtCompound? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtCompound castValue)
			return false;

		value = castValue;
		return true;
	}

	public NbtCompound GetCompound(string name)
	{
		if (!TryGetCompound(name, out var value))
			throw new KeyNotFoundException($"No NbtCompound element named {name}");

		return value;
	}

	public bool TryGetIntArray(string name, out NbtIntArray? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtIntArray castValue)
			return false;

		value = castValue;
		return true;
	}

	public int[] GetIntArray(string name)
	{
		if (!TryGetIntArray(name, out var value))
			throw new KeyNotFoundException($"No NbtIntArray element named {name}");

		return value.Elements;
	}

	public bool TryGetLongArray(string name, out NbtLongArray? value)
	{
		value = null;

		if (!Elements.TryGetValue(name, out var element))
			return false;

		if (element is not NbtLongArray castValue)
			return false;

		value = castValue;
		return true;
	}

	public long[] GetLongArray(string name)
	{
		if (!TryGetLongArray(name, out var value))
			throw new KeyNotFoundException($"No NbtLongArray element named {name}");

		return value.Elements;
	}
}