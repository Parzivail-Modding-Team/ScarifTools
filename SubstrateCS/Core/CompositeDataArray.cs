using System;
using System.Linq;

namespace Substrate.Core;

public class CompositeDataArray3 : IDataArray3
{
    private IDataArray3[] _sections;

    public CompositeDataArray3(IDataArray3[] sections)
    {
        if (sections.Any(section => section == null))
        {
            throw new ArgumentException("sections argument cannot have null entries.");
        }

        if (sections.Any(section => section!.Length != sections[0]!.Length
                                    || section.XDim != sections[0].XDim
                                    || section.YDim != sections[0].YDim
                                    || section.ZDim != sections[0].ZDim))
        {
            throw new ArgumentException("All elements in sections argument must have same metrics.");
        }

        _sections = sections;
    }

    #region IByteArray3 Members

    public int this[int x, int y, int z]
    {
        get
        {
            var ydiv = y / _sections[0].YDim;
            var yrem = y - (ydiv * _sections[0].YDim);
            return _sections[ydiv][x, yrem, z];
        }

        set
        {
            var ydiv = y / _sections[0].YDim;
            var yrem = y - (ydiv * _sections[0].YDim);
            _sections[ydiv][x, yrem, z] = value;
        }
    }

    public int XDim => _sections[0].XDim;

    public int YDim => _sections[0].YDim * _sections.Length;

    public int ZDim => _sections[0].ZDim;

    public int GetIndex(int x, int y, int z)
    {
        var ydiv = y / _sections[0].YDim;
        var yrem = y - (ydiv * _sections[0].YDim);
        return (ydiv * _sections[0].Length) + _sections[ydiv].GetIndex(x, yrem, z);
    }

    public void GetMultiIndex(int index, out int x, out int y, out int z)
    {
        var idiv = index / _sections[0].Length;
        var irem = index - (idiv * _sections[0].Length);
        _sections[idiv].GetMultiIndex(irem, out x, out y, out z);
        y += idiv * _sections[0].YDim;
    }

    #endregion

    #region IByteArray Members

    public int this[int i]
    {
        get
        {
            var idiv = i / _sections[0].Length;
            var irem = i - (idiv * _sections[0].Length);
            return _sections[idiv][irem];
        }
        set
        {
            var idiv = i / _sections[0].Length;
            var irem = i - (idiv * _sections[0].Length);
            _sections[idiv][irem] = value;
        }
    }

    public int Length => _sections[0].Length * _sections.Length;

    public int DataWidth => _sections[0].DataWidth;

    public void Clear()
    {
        foreach (var section in _sections)
            section.Clear();
    }

    #endregion
}