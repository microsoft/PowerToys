using System.Configuration;

namespace PowerAccent.Core.Services;

public class SettingsService : ApplicationSettingsBase
{
    [UserScopedSetting]
    [DefaultSettingValue("Top")]
    public Position Position
    {
        get { return (Position)this[nameof(Position)]; }
        set { this[nameof(Position)] = value; Save(); }
    }

    [UserScopedSetting]
    [DefaultSettingValue("False")]
    public bool UseCaretPosition
    {
        get { return (bool)this[nameof(UseCaretPosition)]; }
        set { this[nameof(UseCaretPosition)] = value; Save(); }
    }

    [UserScopedSetting]
    [DefaultSettingValue("True")]
    public bool IsSpaceBarActive
    {
        get { return (bool)this[nameof(IsSpaceBarActive)]; }
        set { this[nameof(IsSpaceBarActive)] = value; Save(); }
    }

    [UserScopedSetting]
    [DefaultSettingValue("200")]
    public int InputTime
    {
        get { return (int)this[nameof(InputTime)]; }
        set { this[nameof(InputTime)] = value; Save(); }
    }

    #region LetterKey

    [UserScopedSetting]
    public char[] LetterKeyA
    {
        get { return (char[])this[nameof(LetterKeyA)]; }
        set { this[nameof(LetterKeyA)] = value; }
    }

    [UserScopedSetting]
    public char[] LetterKeyC
    {
        get { return (char[])this[nameof(LetterKeyC)]; }
        set { this[nameof(LetterKeyC)] = value; }
    }

    [UserScopedSetting]
    public char[] LetterKeyE
    {
        get { return (char[])this[nameof(LetterKeyE)]; }
        set { this[nameof(LetterKeyE)] = value; }
    }

    [UserScopedSetting]
    public char[] LetterKeyI
    {
        get { return (char[])this[nameof(LetterKeyI)]; }
        set { this[nameof(LetterKeyI)] = value; }
    }

    [UserScopedSetting]
    public char[] LetterKeyO
    {
        get { return (char[])this[nameof(LetterKeyO)]; }
        set { this[nameof(LetterKeyO)] = value; }
    }

    [UserScopedSetting]
    public char[] LetterKeyU
    {
        get { return (char[])this[nameof(LetterKeyU)]; }
        set { this[nameof(LetterKeyU)] = value; }
    }

    [UserScopedSetting]
    public char[] LetterKeyY
    {
        get { return (char[])this[nameof(LetterKeyY)]; }
        set { this[nameof(LetterKeyY)] = value; }
    }

    public void SetLetterKey(LetterKey letter, char[] value)
    {
        string key = $"LetterKey{letter}";
        this[key] = value;
    }

    public char[] GetLetterKey(LetterKey letter)
    {
        string key = $"LetterKey{letter}";
        if (this[key] != null)
            return (char[])this[key];

        return GetDefaultLetterKey(letter);
    }

    public static char[] GetDefaultLetterKey(LetterKey letter)
    {
        switch (letter)
        {
            case LetterKey.A:
                return new char[] { 'à', 'â', 'á', 'ä', 'ã' };
            case LetterKey.C:
                return new char[] { 'ç' };
            case LetterKey.E:
                return new char[] { 'é', 'è', 'ê', 'ë', '€' };
            case LetterKey.I:
                return new char[] { 'î', 'ï', 'í', 'ì' };
            case LetterKey.O:
                return new char[] { 'ô', 'ö', 'ó', 'ò', 'õ' };
            case LetterKey.U:
                return new char[] { 'û', 'ù', 'ü', 'ú' };
            case LetterKey.Y:
                return new char[] { 'ÿ', 'ý' };
        }

        throw new ArgumentException("Letter {0} is missing", letter.ToString());
    }

    #endregion
}

public enum Position
{
    Top,
    Bottom,
    Left,
    Right,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center
}
