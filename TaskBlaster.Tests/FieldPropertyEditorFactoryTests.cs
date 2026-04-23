using TaskBlaster.Views.FieldEditors;

namespace TaskBlaster.Tests;

public class FieldPropertyEditorFactoryTests
{
    [Theory]
    [InlineData("text")]
    [InlineData("textarea")]
    [InlineData("password")]
    [InlineData("email")]
    public void Text_likeTypes_ReturnTextEditor(string type)
    {
        var editor = FieldPropertyEditorFactory.Create(type);
        Assert.IsType<TextPropertyEditor>(editor);
    }

    [Fact]
    public void NumberType_ReturnsNumberEditor()
    {
        Assert.IsType<NumberPropertyEditor>(FieldPropertyEditorFactory.Create("number"));
    }

    [Theory]
    [InlineData("select")]
    [InlineData("multiselect")]
    [InlineData("radio")]
    public void OptionTypes_ReturnOptionsEditor(string type)
    {
        var editor = FieldPropertyEditorFactory.Create(type);
        Assert.IsType<OptionsPropertyEditor>(editor);
    }

    [Theory]
    [InlineData("switch")]
    [InlineData("checkbox")]
    [InlineData("date")]
    [InlineData("time")]
    [InlineData("datetime")]
    [InlineData("color")]
    [InlineData("file")]
    [InlineData("unknown-type")]
    [InlineData("")]
    public void TypesWithNoExtras_ReturnNull(string type)
    {
        Assert.Null(FieldPropertyEditorFactory.Create(type));
    }
}
