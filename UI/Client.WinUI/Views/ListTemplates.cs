using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Fap.Client.WinUI.Views;

internal static class ListTemplates
{
    public static DataTemplate NamedItem(string path) =>
        (DataTemplate)XamlReader.Load(
            "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            $"<TextBlock Text=\"{{Binding {path}}}\" Margin=\"0,4\" TextWrapping=\"NoWrap\" />" +
            "</DataTemplate>");

    public static DataTemplate FileRow() =>
        (DataTemplate)XamlReader.Load(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid ColumnSpacing="12" Padding="0,4">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="*" />
                  <ColumnDefinition Width="80" />
                  <ColumnDefinition Width="110" />
                  <ColumnDefinition Width="140" />
                </Grid.ColumnDefinitions>
                <TextBlock Text="{Binding Name}" TextWrapping="NoWrap" />
                <TextBlock Grid.Column="1" Text="{Binding Kind}" Opacity="0.75" />
                <TextBlock Grid.Column="2" Text="{Binding SizeText}" Opacity="0.75" />
                <TextBlock Grid.Column="3" Text="{Binding ModifiedText}" Opacity="0.75" FontSize="12" />
              </Grid>
            </DataTemplate>
            """);

    public static DataTemplate ShareRow() =>
        (DataTemplate)XamlReader.Load(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid Height="56" Padding="4,0" ColumnSpacing="12"
                    AutomationProperties.Name="{Binding AutomationName}">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="Auto" />
                  <ColumnDefinition Width="*" />
                  <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <FontIcon Glyph="&#xE8B7;" FontSize="20" Opacity="0.7" VerticalAlignment="Center" />
                <StackPanel Grid.Column="1" Spacing="2" VerticalAlignment="Center">
                  <TextBlock Text="{Binding Title}" FontWeight="SemiBold"
                             TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" />
                  <TextBlock Text="{Binding PathText}" Opacity="0.75" FontSize="12"
                             TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" />
                </StackPanel>
                <StackPanel Grid.Column="2" Spacing="2" VerticalAlignment="Center" HorizontalAlignment="Right">
                  <TextBlock Text="{Binding MetaText}" Opacity="0.75" FontSize="12"
                             TextAlignment="Right" TextWrapping="NoWrap" />
                  <TextBlock Text="{Binding StatusText}" Opacity="0.55" FontSize="11"
                             TextAlignment="Right" TextWrapping="NoWrap" />
                </StackPanel>
              </Grid>
            </DataTemplate>
            """);

    public static DataTemplate CompareRow() =>
        (DataTemplate)XamlReader.Load(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid MinHeight="64" Padding="4,8" ColumnSpacing="12"
                    AutomationProperties.Name="{Binding AutomationName}">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="*" />
                  <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <StackPanel Spacing="2" VerticalAlignment="Center">
                  <TextBlock Text="{Binding Title}" FontWeight="SemiBold"
                             TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" />
                  <TextBlock Text="{Binding HardwareText}" Opacity="0.75" FontSize="12"
                             TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" />
                  <TextBlock Text="{Binding MetaText}" Opacity="0.6" FontSize="11"
                             TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" />
                </StackPanel>
                <StackPanel Grid.Column="1" Spacing="2" VerticalAlignment="Center" HorizontalAlignment="Right">
                  <TextBlock Text="{Binding ScoreText}" FontWeight="SemiBold" FontSize="16"
                             TextAlignment="Right" />
                  <TextBlock Text="{Binding StatusText}" Opacity="0.65" FontSize="11"
                             TextAlignment="Right" />
                </StackPanel>
              </Grid>
            </DataTemplate>
            """);

    public static DataTemplate SearchResultRow() =>
        (DataTemplate)XamlReader.Load(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Spacing="2" Padding="0,6">
                <TextBlock Text="{Binding FileName}" FontWeight="SemiBold"
                           TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" />
                <TextBlock Opacity="0.75" FontSize="12" TextWrapping="NoWrap" TextTrimming="CharacterEllipsis">
                  <Run Text="{Binding User}" /><Run Text=" · " /><Run Text="{Binding Path}" />
                </TextBlock>
              </StackPanel>
            </DataTemplate>
            """);

    public static DataTemplate QueueDownloadRow() =>
        (DataTemplate)XamlReader.Load(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Spacing="2" Padding="0,6">
                <TextBlock Text="{Binding FileName}" FontWeight="SemiBold"
                           TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" />
                <TextBlock Opacity="0.75" FontSize="12" TextWrapping="NoWrap" TextTrimming="CharacterEllipsis">
                  <Run Text="{Binding Nickname}" /><Run Text=" · " /><Run Text="{Binding State}" />
                  <Run Text=" · " /><Run Text="{Binding FolderPath}" />
                </TextBlock>
              </StackPanel>
            </DataTemplate>
            """);

    public static DataTemplate PeerRow() =>
        (DataTemplate)XamlReader.Load(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid Height="64" Padding="12,0" ColumnSpacing="12"
                    AutomationProperties.Name="{Binding Title}">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="Auto" />
                  <ColumnDefinition Width="*" />
                  <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <PersonPicture
                    Width="48"
                    Height="48"
                    VerticalAlignment="Center"
                    DisplayName="{Binding DisplayName}"
                    ProfilePicture="{Binding ProfilePicture}" />
                <StackPanel Grid.Column="1" Spacing="2" VerticalAlignment="Center">
                  <TextBlock
                      Text="{Binding Title}"
                      FontWeight="SemiBold"
                      TextWrapping="NoWrap"
                      TextTrimming="CharacterEllipsis" />
                  <TextBlock
                      Text="{Binding Subtitle}"
                      Opacity="0.75"
                      FontSize="12"
                      TextWrapping="NoWrap"
                      TextTrimming="CharacterEllipsis" />
                </StackPanel>
                <FontIcon
                    Grid.Column="2"
                    Glyph="&#xE76C;"
                    FontSize="12"
                    Opacity="0.55"
                    VerticalAlignment="Center"
                    AutomationProperties.Name="Browse shares" />
              </Grid>
            </DataTemplate>
            """);
}
