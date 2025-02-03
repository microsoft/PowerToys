// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace PokedexExtension;

public class Pokemon
{
    public int Number { get; set; }

    public string Name { get; set; }

    public List<string> Types { get; set; }

    public string IconUrl => $"https://serebii.net/pokedex-sv/icon/new/{Number:D3}.png";

    public string SerebiiUrl => $"https://serebii.net/pokedex-sv/{Number:D3}.shtml";

    public Pokemon(int number, string name, List<string> types)
    {
        Number = number;
        Name = name;
        Types = types;
    }
}

internal sealed partial class OpenPokemonCommand : InvokableCommand
{
    public OpenPokemonCommand()
    {
        Name = "Open";
    }

    public override ICommandResult Invoke(object sender)
    {
        if (sender is PokemonListItem item)
        {
            var pokemon = item.Pokemon;
            Process.Start(new ProcessStartInfo(pokemon.SerebiiUrl) { UseShellExecute = true });
        }

        return CommandResult.KeepOpen();
    }
}

internal sealed partial class PokemonListItem : ListItem
{
    private static readonly OpenPokemonCommand _command = new();

    public Pokemon Pokemon { get; private set; }

    public PokemonListItem(Pokemon p)
        : base(_command)
    {
        Pokemon = p;
        Title = Pokemon.Name;
        Icon = new(Pokemon.IconUrl);
        Subtitle = $"#{Pokemon.Number}";
        Tags = Pokemon.Types.Select(t => new Tag() { Text = t, Background = PokedexExtensionPage.GetColorForType(t) }).ToArray();
    }
}

internal sealed partial class PokedexExtensionPage : ListPage
{
    private readonly List<Pokemon> _kanto =
    [
        new Pokemon(1, "Bulbasaur", ["Grass", "Poison"]),
        new Pokemon(2, "Ivysaur", ["Grass", "Poison"]),
        new Pokemon(3, "Venusaur", ["Grass", "Poison"]),
        new Pokemon(4, "Charmander", ["Fire"]),
        new Pokemon(5, "Charmeleon", ["Fire"]),
        new Pokemon(6, "Charizard", ["Fire", "Flying"]),
        new Pokemon(7, "Squirtle", ["Water"]),
        new Pokemon(8, "Wartortle", ["Water"]),
        new Pokemon(9, "Blastoise", ["Water"]),
        new Pokemon(10, "Caterpie", ["Bug"]),
        new Pokemon(11, "Metapod", ["Bug"]),
        new Pokemon(12, "Butterfree", ["Bug", "Flying"]),
        new Pokemon(13, "Weedle", ["Bug", "Poison"]),
        new Pokemon(14, "Kakuna", ["Bug", "Poison"]),
        new Pokemon(15, "Beedrill", ["Bug", "Poison"]),
        new Pokemon(16, "Pidgey", ["Normal", "Flying"]),
        new Pokemon(17, "Pidgeotto", ["Normal", "Flying"]),
        new Pokemon(18, "Pidgeot", ["Normal", "Flying"]),
        new Pokemon(19, "Rattata", ["Normal"]),
        new Pokemon(20, "Raticate", ["Normal"]),
        new Pokemon(21, "Spearow", ["Normal", "Flying"]),
        new Pokemon(22, "Fearow", ["Normal", "Flying"]),
        new Pokemon(23, "Ekans", ["Poison"]),
        new Pokemon(24, "Arbok", ["Poison"]),
        new Pokemon(25, "Pikachu", ["Electric"]),
        new Pokemon(26, "Raichu", ["Electric"]),
        new Pokemon(27, "Sandshrew", ["Ground"]),
        new Pokemon(28, "Sandslash", ["Ground"]),
        new Pokemon(29, "Nidoran♀", ["Poison"]),
        new Pokemon(30, "Nidorina", ["Poison"]),
        new Pokemon(31, "Nidoqueen", ["Poison", "Ground"]),
        new Pokemon(32, "Nidoran♂", ["Poison"]),
        new Pokemon(33, "Nidorino", ["Poison"]),
        new Pokemon(34, "Nidoking", ["Poison", "Ground"]),
        new Pokemon(35, "Clefairy", ["Fairy"]),
        new Pokemon(36, "Clefable", ["Fairy"]),
        new Pokemon(37, "Vulpix", ["Fire"]),
        new Pokemon(38, "Ninetales", ["Fire"]),
        new Pokemon(39, "Jigglypuff", ["Normal", "Fairy"]),
        new Pokemon(40, "Wigglytuff", ["Normal", "Fairy"]),
        new Pokemon(41, "Zubat", ["Poison", "Flying"]),
        new Pokemon(42, "Golbat", ["Poison", "Flying"]),
        new Pokemon(43, "Oddish", ["Grass", "Poison"]),
        new Pokemon(44, "Gloom", ["Grass", "Poison"]),
        new Pokemon(45, "Vileplume", ["Grass", "Poison"]),
        new Pokemon(46, "Paras", ["Bug", "Grass"]),
        new Pokemon(47, "Parasect", ["Bug", "Grass"]),
        new Pokemon(48, "Venonat", ["Bug", "Poison"]),
        new Pokemon(49, "Venomoth", ["Bug", "Poison"]),
        new Pokemon(50, "Diglett", ["Ground"]),
        new Pokemon(51, "Dugtrio", ["Ground"]),
        new Pokemon(52, "Meowth", ["Normal"]),
        new Pokemon(53, "Persian", ["Normal"]),
        new Pokemon(54, "Psyduck", ["Water"]),
        new Pokemon(55, "Golduck", ["Water"]),
        new Pokemon(56, "Mankey", ["Fighting"]),
        new Pokemon(57, "Primeape", ["Fighting"]),
        new Pokemon(58, "Growlithe", ["Fire"]),
        new Pokemon(59, "Arcanine", ["Fire"]),
        new Pokemon(60, "Poliwag", ["Water"]),
        new Pokemon(61, "Poliwhirl", ["Water"]),
        new Pokemon(62, "Poliwrath", ["Water", "Fighting"]),
        new Pokemon(63, "Abra", ["Psychic"]),
        new Pokemon(64, "Kadabra", ["Psychic"]),
        new Pokemon(65, "Alakazam", ["Psychic"]),
        new Pokemon(66, "Machop", ["Fighting"]),
        new Pokemon(67, "Machoke", ["Fighting"]),
        new Pokemon(68, "Machamp", ["Fighting"]),
        new Pokemon(69, "Bellsprout", ["Grass", "Poison"]),
        new Pokemon(70, "Weepinbell", ["Grass", "Poison"]),
        new Pokemon(71, "Victreebel", ["Grass", "Poison"]),
        new Pokemon(72, "Tentacool", ["Water", "Poison"]),
        new Pokemon(73, "Tentacruel", ["Water", "Poison"]),
        new Pokemon(74, "Geodude", ["Rock", "Ground"]),
        new Pokemon(75, "Graveler", ["Rock", "Ground"]),
        new Pokemon(76, "Golem", ["Rock", "Ground"]),
        new Pokemon(77, "Ponyta", ["Fire"]),
        new Pokemon(78, "Rapidash", ["Fire"]),
        new Pokemon(79, "Slowpoke", ["Water", "Psychic"]),
        new Pokemon(80, "Slowbro", ["Water", "Psychic"]),
        new Pokemon(81, "Magnemite", ["Electric", "Steel"]),
        new Pokemon(82, "Magneton", ["Electric", "Steel"]),
        new Pokemon(83, "Farfetch'd", ["Normal", "Flying"]),
        new Pokemon(84, "Doduo", ["Normal", "Flying"]),
        new Pokemon(85, "Dodrio", ["Normal", "Flying"]),
        new Pokemon(86, "Seel", ["Water"]),
        new Pokemon(87, "Dewgong", ["Water", "Ice"]),
        new Pokemon(88, "Grimer", ["Poison"]),
        new Pokemon(89, "Muk", ["Poison"]),
        new Pokemon(90, "Shellder", ["Water"]),
        new Pokemon(91, "Cloyster", ["Water", "Ice"]),
        new Pokemon(92, "Gastly", ["Ghost", "Poison"]),
        new Pokemon(93, "Haunter", ["Ghost", "Poison"]),
        new Pokemon(94, "Gengar", ["Ghost", "Poison"]),
        new Pokemon(95, "Onix", ["Rock", "Ground"]),
        new Pokemon(96, "Drowzee", ["Psychic"]),
        new Pokemon(97, "Hypno", ["Psychic"]),
        new Pokemon(98, "Krabby", ["Water"]),
        new Pokemon(99, "Kingler", ["Water"]),
        new Pokemon(100, "Voltorb", ["Electric"]),
        new Pokemon(101, "Electrode", ["Electric"]),
        new Pokemon(102, "Exeggcute", ["Grass", "Psychic"]),
        new Pokemon(103, "Exeggutor", ["Grass", "Psychic"]),
        new Pokemon(104, "Cubone", ["Ground"]),
        new Pokemon(105, "Marowak", ["Ground"]),
        new Pokemon(106, "Hitmonlee", ["Fighting"]),
        new Pokemon(107, "Hitmonchan", ["Fighting"]),
        new Pokemon(108, "Lickitung", ["Normal"]),
        new Pokemon(109, "Koffing", ["Poison"]),
        new Pokemon(110, "Weezing", ["Poison"]),
        new Pokemon(111, "Rhyhorn", ["Ground", "Rock"]),
        new Pokemon(112, "Rhydon", ["Ground", "Rock"]),
        new Pokemon(113, "Chansey", ["Normal"]),
        new Pokemon(114, "Tangela", ["Grass"]),
        new Pokemon(115, "Kangaskhan", ["Normal"]),
        new Pokemon(116, "Horsea", ["Water"]),
        new Pokemon(117, "Seadra", ["Water"]),
        new Pokemon(118, "Goldeen", ["Water"]),
        new Pokemon(119, "Seaking", ["Water"]),
        new Pokemon(120, "Staryu", ["Water"]),
        new Pokemon(121, "Starmie", ["Water", "Psychic"]),
        new Pokemon(122, "Mr. Mime", ["Psychic", "Fairy"]),
        new Pokemon(123, "Scyther", ["Bug", "Flying"]),
        new Pokemon(124, "Jynx", ["Ice", "Psychic"]),
        new Pokemon(125, "Electabuzz", ["Electric"]),
        new Pokemon(126, "Magmar", ["Fire"]),
        new Pokemon(127, "Pinsir", ["Bug"]),
        new Pokemon(128, "Tauros", ["Normal"]),
        new Pokemon(129, "Magikarp", ["Water"]),
        new Pokemon(130, "Gyarados", ["Water", "Flying"]),
        new Pokemon(131, "Lapras", ["Water", "Ice"]),
        new Pokemon(132, "Ditto", ["Normal"]),
        new Pokemon(133, "Eevee", ["Normal"]),
        new Pokemon(134, "Vaporeon", ["Water"]),
        new Pokemon(135, "Jolteon", ["Electric"]),
        new Pokemon(136, "Flareon", ["Fire"]),
        new Pokemon(137, "Porygon", ["Normal"]),
        new Pokemon(138, "Omanyte", ["Rock", "Water"]),
        new Pokemon(139, "Omastar", ["Rock", "Water"]),
        new Pokemon(140, "Kabuto", ["Rock", "Water"]),
        new Pokemon(141, "Kabutops", ["Rock", "Water"]),
        new Pokemon(142, "Aerodactyl", ["Rock", "Flying"]),
        new Pokemon(143, "Snorlax", ["Normal"]),
        new Pokemon(144, "Articuno", ["Ice", "Flying"]),
        new Pokemon(145, "Zapdos", ["Electric", "Flying"]),
        new Pokemon(146, "Moltres", ["Fire", "Flying"]),
        new Pokemon(147, "Dratini", ["Dragon"]),
        new Pokemon(148, "Dragonair", ["Dragon"]),
        new Pokemon(149, "Dragonite", ["Dragon", "Flying"]),
        new Pokemon(150, "Mewtwo", ["Psychic"]),
        new Pokemon(151, "Mew", ["Psychic"]),
    ];

    private readonly List<Pokemon> _johto =
    [
        new Pokemon(152, "Chikorita", ["Grass"]),
        new Pokemon(153, "Bayleef", ["Grass"]),
        new Pokemon(154, "Meganium", ["Grass"]),
        new Pokemon(155, "Cyndaquil", ["Fire"]),
        new Pokemon(156, "Quilava", ["Fire"]),
        new Pokemon(157, "Typhlosion", ["Fire"]),
        new Pokemon(158, "Totodile", ["Water"]),
        new Pokemon(159, "Croconaw", ["Water"]),
        new Pokemon(160, "Feraligatr", ["Water"]),
        new Pokemon(161, "Sentret", ["Normal"]),
        new Pokemon(162, "Furret", ["Normal"]),
        new Pokemon(163, "Hoothoot", ["Normal", "Flying"]),
        new Pokemon(164, "Noctowl", ["Normal", "Flying"]),
        new Pokemon(165, "Ledyba", ["Bug", "Flying"]),
        new Pokemon(166, "Ledian", ["Bug", "Flying"]),
        new Pokemon(167, "Spinarak", ["Bug", "Poison"]),
        new Pokemon(168, "Ariados", ["Bug", "Poison"]),
        new Pokemon(169, "Crobat", ["Poison", "Flying"]),
        new Pokemon(170, "Chinchou", ["Water", "Electric"]),
        new Pokemon(171, "Lanturn", ["Water", "Electric"]),
        new Pokemon(172, "Pichu", ["Electric"]),
        new Pokemon(173, "Cleffa", ["Fairy"]),
        new Pokemon(174, "Igglybuff", ["Normal", "Fairy"]),
        new Pokemon(175, "Togepi", ["Fairy"]),
        new Pokemon(176, "Togetic", ["Fairy", "Flying"]),
        new Pokemon(177, "Natu", ["Psychic", "Flying"]),
        new Pokemon(178, "Xatu", ["Psychic", "Flying"]),
        new Pokemon(179, "Mareep", ["Electric"]),
        new Pokemon(180, "Flaaffy", ["Electric"]),
        new Pokemon(181, "Ampharos", ["Electric"]),
        new Pokemon(182, "Bellossom", ["Grass"]),
        new Pokemon(183, "Marill", ["Water", "Fairy"]),
        new Pokemon(184, "Azumarill", ["Water", "Fairy"]),
        new Pokemon(185, "Sudowoodo", ["Rock"]),
        new Pokemon(186, "Politoed", ["Water"]),
        new Pokemon(187, "Hoppip", ["Grass", "Flying"]),
        new Pokemon(188, "Skiploom", ["Grass", "Flying"]),
        new Pokemon(189, "Jumpluff", ["Grass", "Flying"]),
        new Pokemon(190, "Aipom", ["Normal"]),
        new Pokemon(191, "Sunkern", ["Grass"]),
        new Pokemon(192, "Sunflora", ["Grass"]),
        new Pokemon(193, "Yanma", ["Bug", "Flying"]),
        new Pokemon(194, "Wooper", ["Water", "Ground"]),
        new Pokemon(195, "Quagsire", ["Water", "Ground"]),
        new Pokemon(196, "Espeon", ["Psychic"]),
        new Pokemon(197, "Umbreon", ["Dark"]),
        new Pokemon(198, "Murkrow", ["Dark", "Flying"]),
        new Pokemon(199, "Slowking", ["Water", "Psychic"]),
        new Pokemon(200, "Misdreavus", ["Ghost"]),
        new Pokemon(201, "Unown", ["Psychic"]),
        new Pokemon(202, "Wobbuffet", ["Psychic"]),
        new Pokemon(203, "Girafarig", ["Normal", "Psychic"]),
        new Pokemon(204, "Pineco", ["Bug"]),
        new Pokemon(205, "Forretress", ["Bug", "Steel"]),
        new Pokemon(206, "Dunsparce", ["Normal"]),
        new Pokemon(207, "Gligar", ["Ground", "Flying"]),
        new Pokemon(208, "Steelix", ["Steel", "Ground"]),
        new Pokemon(209, "Snubbull", ["Fairy"]),
        new Pokemon(210, "Granbull", ["Fairy"]),
        new Pokemon(211, "Qwilfish", ["Water", "Poison"]),
        new Pokemon(212, "Scizor", ["Bug", "Steel"]),
        new Pokemon(213, "Shuckle", ["Bug", "Rock"]),
        new Pokemon(214, "Heracross", ["Bug", "Fighting"]),
        new Pokemon(215, "Sneasel", ["Dark", "Ice"]),
        new Pokemon(216, "Teddiursa", ["Normal"]),
        new Pokemon(217, "Ursaring", ["Normal"]),
        new Pokemon(218, "Slugma", ["Fire"]),
        new Pokemon(219, "Magcargo", ["Fire", "Rock"]),
        new Pokemon(220, "Swinub", ["Ice", "Ground"]),
        new Pokemon(221, "Piloswine", ["Ice", "Ground"]),
        new Pokemon(222, "Corsola", ["Water", "Rock"]),
        new Pokemon(223, "Remoraid", ["Water"]),
        new Pokemon(224, "Octillery", ["Water"]),
        new Pokemon(225, "Delibird", ["Ice", "Flying"]),
        new Pokemon(226, "Mantine", ["Water", "Flying"]),
        new Pokemon(227, "Skarmory", ["Steel", "Flying"]),
        new Pokemon(228, "Houndour", ["Dark", "Fire"]),
        new Pokemon(229, "Houndoom", ["Dark", "Fire"]),
        new Pokemon(230, "Kingdra", ["Water", "Dragon"]),
        new Pokemon(231, "Phanpy", ["Ground"]),
        new Pokemon(232, "Donphan", ["Ground"]),
        new Pokemon(233, "Porygon2", ["Normal"]),
        new Pokemon(234, "Stantler", ["Normal"]),
        new Pokemon(235, "Smeargle", ["Normal"]),
        new Pokemon(236, "Tyrogue", ["Fighting"]),
        new Pokemon(237, "Hitmontop", ["Fighting"]),
        new Pokemon(238, "Smoochum", ["Ice", "Psychic"]),
        new Pokemon(239, "Elekid", ["Electric"]),
        new Pokemon(240, "Magby", ["Fire"]),
        new Pokemon(241, "Miltank", ["Normal"]),
        new Pokemon(242, "Blissey", ["Normal"]),
        new Pokemon(243, "Raikou", ["Electric"]),
        new Pokemon(244, "Entei", ["Fire"]),
        new Pokemon(245, "Suicune", ["Water"]),
        new Pokemon(246, "Larvitar", ["Rock", "Ground"]),
        new Pokemon(247, "Pupitar", ["Rock", "Ground"]),
        new Pokemon(248, "Tyranitar", ["Rock", "Dark"]),
        new Pokemon(249, "Lugia", ["Psychic", "Flying"]),
        new Pokemon(250, "Ho-Oh", ["Fire", "Flying"]),
        new Pokemon(251, "Celebi", ["Psychic", "Grass"]),
    ];

    private readonly List<Pokemon> _hoenn =
    [
        new Pokemon(252, "Treecko", ["Grass"]),
        new Pokemon(253, "Grovyle", ["Grass"]),
        new Pokemon(254, "Sceptile", ["Grass"]),
        new Pokemon(255, "Torchic", ["Fire"]),
        new Pokemon(256, "Combusken", ["Fire", "Fighting"]),
        new Pokemon(257, "Blaziken", ["Fire", "Fighting"]),
        new Pokemon(258, "Mudkip", ["Water"]),
        new Pokemon(259, "Marshtomp", ["Water", "Ground"]),
        new Pokemon(260, "Swampert", ["Water", "Ground"]),
        new Pokemon(261, "Poochyena", ["Dark"]),
        new Pokemon(262, "Mightyena", ["Dark"]),
        new Pokemon(263, "Zigzagoon", ["Normal"]),
        new Pokemon(264, "Linoone", ["Normal"]),
        new Pokemon(265, "Wurmple", ["Bug"]),
        new Pokemon(266, "Silcoon", ["Bug"]),
        new Pokemon(267, "Beautifly", ["Bug", "Flying"]),
        new Pokemon(268, "Cascoon", ["Bug"]),
        new Pokemon(269, "Dustox", ["Bug", "Poison"]),
        new Pokemon(270, "Lotad", ["Water", "Grass"]),
        new Pokemon(271, "Lombre", ["Water", "Grass"]),
        new Pokemon(272, "Ludicolo", ["Water", "Grass"]),
        new Pokemon(273, "Seedot", ["Grass"]),
        new Pokemon(274, "Nuzleaf", ["Grass", "Dark"]),
        new Pokemon(275, "Shiftry", ["Grass", "Dark"]),
        new Pokemon(276, "Taillow", ["Normal", "Flying"]),
        new Pokemon(277, "Swellow", ["Normal", "Flying"]),
        new Pokemon(278, "Wingull", ["Water", "Flying"]),
        new Pokemon(279, "Pelipper", ["Water", "Flying"]),
        new Pokemon(280, "Ralts", ["Psychic", "Fairy"]),
        new Pokemon(281, "Kirlia", ["Psychic", "Fairy"]),
        new Pokemon(282, "Gardevoir", ["Psychic", "Fairy"]),
        new Pokemon(283, "Surskit", ["Bug", "Water"]),
        new Pokemon(284, "Masquerain", ["Bug", "Flying"]),
        new Pokemon(285, "Shroomish", ["Grass"]),
        new Pokemon(286, "Breloom", ["Grass", "Fighting"]),
        new Pokemon(287, "Slakoth", ["Normal"]),
        new Pokemon(288, "Vigoroth", ["Normal"]),
        new Pokemon(289, "Slaking", ["Normal"]),
        new Pokemon(290, "Nincada", ["Bug", "Ground"]),
        new Pokemon(291, "Ninjask", ["Bug", "Flying"]),
        new Pokemon(292, "Shedinja", ["Bug", "Ghost"]),
        new Pokemon(293, "Whismur", ["Normal"]),
        new Pokemon(294, "Loudred", ["Normal"]),
        new Pokemon(295, "Exploud", ["Normal"]),
        new Pokemon(296, "Makuhita", ["Fighting"]),
        new Pokemon(297, "Hariyama", ["Fighting"]),
        new Pokemon(298, "Azurill", ["Normal", "Fairy"]),
        new Pokemon(299, "Nosepass", ["Rock"]),
        new Pokemon(300, "Skitty", ["Normal"]),
        new Pokemon(301, "Delcatty", ["Normal"]),
        new Pokemon(302, "Sableye", ["Dark", "Ghost"]),
        new Pokemon(303, "Mawile", ["Steel", "Fairy"]),
        new Pokemon(304, "Aron", ["Steel", "Rock"]),
        new Pokemon(305, "Lairon", ["Steel", "Rock"]),
        new Pokemon(306, "Aggron", ["Steel", "Rock"]),
        new Pokemon(307, "Meditite", ["Fighting", "Psychic"]),
        new Pokemon(308, "Medicham", ["Fighting", "Psychic"]),
        new Pokemon(309, "Electrike", ["Electric"]),
        new Pokemon(310, "Manectric", ["Electric"]),
        new Pokemon(311, "Plusle", ["Electric"]),
        new Pokemon(312, "Minun", ["Electric"]),
        new Pokemon(313, "Volbeat", ["Bug"]),
        new Pokemon(314, "Illumise", ["Bug"]),
        new Pokemon(315, "Roselia", ["Grass", "Poison"]),
        new Pokemon(316, "Gulpin", ["Poison"]),
        new Pokemon(317, "Swalot", ["Poison"]),
        new Pokemon(318, "Carvanha", ["Water", "Dark"]),
        new Pokemon(319, "Sharpedo", ["Water", "Dark"]),
        new Pokemon(320, "Wailmer", ["Water"]),
        new Pokemon(321, "Wailord", ["Water"]),
        new Pokemon(322, "Numel", ["Fire", "Ground"]),
        new Pokemon(323, "Camerupt", ["Fire", "Ground"]),
        new Pokemon(324, "Torkoal", ["Fire"]),
        new Pokemon(325, "Spoink", ["Psychic"]),
        new Pokemon(326, "Grumpig", ["Psychic"]),
        new Pokemon(327, "Spinda", ["Normal"]),
        new Pokemon(328, "Trapinch", ["Ground"]),
        new Pokemon(329, "Vibrava", ["Ground", "Dragon"]),
        new Pokemon(330, "Flygon", ["Ground", "Dragon"]),
        new Pokemon(331, "Cacnea", ["Grass"]),
        new Pokemon(332, "Cacturne", ["Grass", "Dark"]),
        new Pokemon(333, "Swablu", ["Normal", "Flying"]),
        new Pokemon(334, "Altaria", ["Dragon", "Flying"]),
        new Pokemon(335, "Zangoose", ["Normal"]),
        new Pokemon(336, "Seviper", ["Poison"]),
        new Pokemon(337, "Lunatone", ["Rock", "Psychic"]),
        new Pokemon(338, "Solrock", ["Rock", "Psychic"]),
        new Pokemon(339, "Barboach", ["Water", "Ground"]),
        new Pokemon(340, "Whiscash", ["Water", "Ground"]),
        new Pokemon(341, "Corphish", ["Water"]),
        new Pokemon(342, "Crawdaunt", ["Water", "Dark"]),
        new Pokemon(343, "Baltoy", ["Ground", "Psychic"]),
        new Pokemon(344, "Claydol", ["Ground", "Psychic"]),
        new Pokemon(345, "Lileep", ["Rock", "Grass"]),
        new Pokemon(346, "Cradily", ["Rock", "Grass"]),
        new Pokemon(347, "Anorith", ["Rock", "Bug"]),
        new Pokemon(348, "Armaldo", ["Rock", "Bug"]),
        new Pokemon(349, "Feebas", ["Water"]),
        new Pokemon(350, "Milotic", ["Water"]),
        new Pokemon(351, "Castform", ["Normal"]),
        new Pokemon(352, "Kecleon", ["Normal"]),
        new Pokemon(353, "Shuppet", ["Ghost"]),
        new Pokemon(354, "Banette", ["Ghost"]),
        new Pokemon(355, "Duskull", ["Ghost"]),
        new Pokemon(356, "Dusclops", ["Ghost"]),
        new Pokemon(357, "Tropius", ["Grass", "Flying"]),
        new Pokemon(358, "Chimecho", ["Psychic"]),
        new Pokemon(359, "Absol", ["Dark"]),
        new Pokemon(360, "Wynaut", ["Psychic"]),
        new Pokemon(361, "Snorunt", ["Ice"]),
        new Pokemon(362, "Glalie", ["Ice"]),
        new Pokemon(363, "Spheal", ["Ice", "Water"]),
        new Pokemon(364, "Sealeo", ["Ice", "Water"]),
        new Pokemon(365, "Walrein", ["Ice", "Water"]),
        new Pokemon(366, "Clamperl", ["Water"]),
        new Pokemon(367, "Huntail", ["Water"]),
        new Pokemon(368, "Gorebyss", ["Water"]),
        new Pokemon(369, "Relicanth", ["Water", "Rock"]),
        new Pokemon(370, "Luvdisc", ["Water"]),
        new Pokemon(371, "Bagon", ["Dragon"]),
        new Pokemon(372, "Shelgon", ["Dragon"]),
        new Pokemon(373, "Salamence", ["Dragon", "Flying"]),
        new Pokemon(374, "Beldum", ["Steel", "Psychic"]),
        new Pokemon(375, "Metang", ["Steel", "Psychic"]),
        new Pokemon(376, "Metagross", ["Steel", "Psychic"]),
        new Pokemon(377, "Regirock", ["Rock"]),
        new Pokemon(378, "Regice", ["Ice"]),
        new Pokemon(379, "Registeel", ["Steel"]),
        new Pokemon(380, "Latias", ["Dragon", "Psychic"]),
        new Pokemon(381, "Latios", ["Dragon", "Psychic"]),
        new Pokemon(382, "Kyogre", ["Water"]),
        new Pokemon(383, "Groudon", ["Ground"]),
        new Pokemon(384, "Rayquaza", ["Dragon", "Flying"]),
        new Pokemon(385, "Jirachi", ["Steel", "Psychic"]),
        new Pokemon(386, "Deoxys", ["Psychic"]),
    ];

    public PokedexExtensionPage()
    {
        Icon = new("https://e7.pngegg.com/pngimages/311/5/png-clipart-pokedex-pokemon-go-hoenn-pokemon-x-and-y-hoenn-pokedex-pokemon-ash-thumbnail.png");
        Name = "Pokedex";
    }

    public override IListItem[] GetItems() => _kanto.AsEnumerable().Concat(_johto.AsEnumerable()).Concat(_hoenn.AsEnumerable()).Select(GetPokemonListItem).ToArray();

    private static ListItem GetPokemonListItem(Pokemon pokemon) => new PokemonListItem(pokemon);

    // Dictionary mapping Pokémon types to their corresponding colors
    private static readonly Dictionary<string, OptionalColor> TypeColors = new()
    {
        { "Normal", ColorHelpers.FromArgb(255, 168, 168, 120) },   // Light Brownish Grey
        { "Fire", ColorHelpers.FromArgb(255, 240, 128, 48) },      // Orange-Red
        { "Water", ColorHelpers.FromArgb(255, 104, 144, 240) },    // Medium Blue
        { "Electric", ColorHelpers.FromArgb(255, 248, 208, 48) },  // Yellow
        { "Grass", ColorHelpers.FromArgb(255, 120, 200, 80) },     // Green
        { "Ice", ColorHelpers.FromArgb(255, 152, 216, 216) },      // Cyan
        { "Fighting", ColorHelpers.FromArgb(255, 192, 48, 40) },   // Red
        { "Poison", ColorHelpers.FromArgb(255, 160, 64, 160) },    // Purple
        { "Ground", ColorHelpers.FromArgb(255, 224, 192, 104) },   // Yellowish Brown
        { "Flying", ColorHelpers.FromArgb(255, 168, 144, 240) },   // Light Blue
        { "Psychic", ColorHelpers.FromArgb(255, 248, 88, 136) },   // Pink
        { "Bug", ColorHelpers.FromArgb(255, 168, 184, 32) },       // Greenish Yellow
        { "Rock", ColorHelpers.FromArgb(255, 184, 160, 56) },      // Brown
        { "Ghost", ColorHelpers.FromArgb(255, 112, 88, 152) },     // Dark Purple
        { "Dragon", ColorHelpers.FromArgb(255, 112, 56, 248) },    // Blue-Violet
        { "Dark", ColorHelpers.FromArgb(255, 112, 88, 72) },       // Dark Brown
        { "Steel", ColorHelpers.FromArgb(255, 184, 184, 208) },    // Light Grey
        { "Fairy", ColorHelpers.FromArgb(255, 238, 153, 172) },    // Light Pink
    };

    // Method to get the color for a given type
    public static OptionalColor GetColorForType(string type) =>

        // Check if the type exists in the dictionary
        TypeColors.TryGetValue(type, out var color) ? color : ColorHelpers.NoColor();
}
