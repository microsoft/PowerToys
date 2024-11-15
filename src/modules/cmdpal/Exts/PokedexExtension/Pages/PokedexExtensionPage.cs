// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.UI;

namespace PokedexExtension;

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "This is sample code")]
public class Pokemon
{
    public int Number { get; set; }

    public string Name { get; set; }

    public List<string> Types { get; set; }

    public string IconUrl => $"https://serebii.net/pokedex-sv/icon/new/{Number:D3}.png";

    public Pokemon(int number, string name, List<string> types)
    {
        Number = number;
        Name = name;
        Types = types;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed partial class PokemonPage : NoOpCommand
{
    public PokemonPage(Pokemon pokemon)
    {
        Name = pokemon.Name;
        Icon = new(pokemon.IconUrl);
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed partial class PokedexExtensionPage : ListPage
{
    private readonly List<Pokemon> _kanto = new()
    {
        new Pokemon(1, "Bulbasaur", new List<string> { "Grass", "Poison" }),
        new Pokemon(2, "Ivysaur", new List<string> { "Grass", "Poison" }),
        new Pokemon(3, "Venusaur", new List<string> { "Grass", "Poison" }),
        new Pokemon(4, "Charmander", new List<string> { "Fire" }),
        new Pokemon(5, "Charmeleon", new List<string> { "Fire" }),
        new Pokemon(6, "Charizard", new List<string> { "Fire", "Flying" }),
        new Pokemon(7, "Squirtle", new List<string> { "Water" }),
        new Pokemon(8, "Wartortle", new List<string> { "Water" }),
        new Pokemon(9, "Blastoise", new List<string> { "Water" }),
        new Pokemon(10, "Caterpie", new List<string> { "Bug" }),
        new Pokemon(11, "Metapod", new List<string> { "Bug" }),
        new Pokemon(12, "Butterfree", new List<string> { "Bug", "Flying" }),
        new Pokemon(13, "Weedle", new List<string> { "Bug", "Poison" }),
        new Pokemon(14, "Kakuna", new List<string> { "Bug", "Poison" }),
        new Pokemon(15, "Beedrill", new List<string> { "Bug", "Poison" }),
        new Pokemon(16, "Pidgey", new List<string> { "Normal", "Flying" }),
        new Pokemon(17, "Pidgeotto", new List<string> { "Normal", "Flying" }),
        new Pokemon(18, "Pidgeot", new List<string> { "Normal", "Flying" }),
        new Pokemon(19, "Rattata", new List<string> { "Normal" }),
        new Pokemon(20, "Raticate", new List<string> { "Normal" }),
        new Pokemon(21, "Spearow", new List<string> { "Normal", "Flying" }),
        new Pokemon(22, "Fearow", new List<string> { "Normal", "Flying" }),
        new Pokemon(23, "Ekans", new List<string> { "Poison" }),
        new Pokemon(24, "Arbok", new List<string> { "Poison" }),
        new Pokemon(25, "Pikachu", new List<string> { "Electric" }),
        new Pokemon(26, "Raichu", new List<string> { "Electric" }),
        new Pokemon(27, "Sandshrew", new List<string> { "Ground" }),
        new Pokemon(28, "Sandslash", new List<string> { "Ground" }),
        new Pokemon(29, "Nidoran♀", new List<string> { "Poison" }),
        new Pokemon(30, "Nidorina", new List<string> { "Poison" }),
        new Pokemon(31, "Nidoqueen", new List<string> { "Poison", "Ground" }),
        new Pokemon(32, "Nidoran♂", new List<string> { "Poison" }),
        new Pokemon(33, "Nidorino", new List<string> { "Poison" }),
        new Pokemon(34, "Nidoking", new List<string> { "Poison", "Ground" }),
        new Pokemon(35, "Clefairy", new List<string> { "Fairy" }),
        new Pokemon(36, "Clefable", new List<string> { "Fairy" }),
        new Pokemon(37, "Vulpix", new List<string> { "Fire" }),
        new Pokemon(38, "Ninetales", new List<string> { "Fire" }),
        new Pokemon(39, "Jigglypuff", new List<string> { "Normal", "Fairy" }),
        new Pokemon(40, "Wigglytuff", new List<string> { "Normal", "Fairy" }),
        new Pokemon(41, "Zubat", new List<string> { "Poison", "Flying" }),
        new Pokemon(42, "Golbat", new List<string> { "Poison", "Flying" }),
        new Pokemon(43, "Oddish", new List<string> { "Grass", "Poison" }),
        new Pokemon(44, "Gloom", new List<string> { "Grass", "Poison" }),
        new Pokemon(45, "Vileplume", new List<string> { "Grass", "Poison" }),
        new Pokemon(46, "Paras", new List<string> { "Bug", "Grass" }),
        new Pokemon(47, "Parasect", new List<string> { "Bug", "Grass" }),
        new Pokemon(48, "Venonat", new List<string> { "Bug", "Poison" }),
        new Pokemon(49, "Venomoth", new List<string> { "Bug", "Poison" }),
        new Pokemon(50, "Diglett", new List<string> { "Ground" }),
        new Pokemon(51, "Dugtrio", new List<string> { "Ground" }),
        new Pokemon(52, "Meowth", new List<string> { "Normal" }),
        new Pokemon(53, "Persian", new List<string> { "Normal" }),
        new Pokemon(54, "Psyduck", new List<string> { "Water" }),
        new Pokemon(55, "Golduck", new List<string> { "Water" }),
        new Pokemon(56, "Mankey", new List<string> { "Fighting" }),
        new Pokemon(57, "Primeape", new List<string> { "Fighting" }),
        new Pokemon(58, "Growlithe", new List<string> { "Fire" }),
        new Pokemon(59, "Arcanine", new List<string> { "Fire" }),
        new Pokemon(60, "Poliwag", new List<string> { "Water" }),
        new Pokemon(61, "Poliwhirl", new List<string> { "Water" }),
        new Pokemon(62, "Poliwrath", new List<string> { "Water", "Fighting" }),
        new Pokemon(63, "Abra", new List<string> { "Psychic" }),
        new Pokemon(64, "Kadabra", new List<string> { "Psychic" }),
        new Pokemon(65, "Alakazam", new List<string> { "Psychic" }),
        new Pokemon(66, "Machop", new List<string> { "Fighting" }),
        new Pokemon(67, "Machoke", new List<string> { "Fighting" }),
        new Pokemon(68, "Machamp", new List<string> { "Fighting" }),
        new Pokemon(69, "Bellsprout", new List<string> { "Grass", "Poison" }),
        new Pokemon(70, "Weepinbell", new List<string> { "Grass", "Poison" }),
        new Pokemon(71, "Victreebel", new List<string> { "Grass", "Poison" }),
        new Pokemon(72, "Tentacool", new List<string> { "Water", "Poison" }),
        new Pokemon(73, "Tentacruel", new List<string> { "Water", "Poison" }),
        new Pokemon(74, "Geodude", new List<string> { "Rock", "Ground" }),
        new Pokemon(75, "Graveler", new List<string> { "Rock", "Ground" }),
        new Pokemon(76, "Golem", new List<string> { "Rock", "Ground" }),
        new Pokemon(77, "Ponyta", new List<string> { "Fire" }),
        new Pokemon(78, "Rapidash", new List<string> { "Fire" }),
        new Pokemon(79, "Slowpoke", new List<string> { "Water", "Psychic" }),
        new Pokemon(80, "Slowbro", new List<string> { "Water", "Psychic" }),
        new Pokemon(81, "Magnemite", new List<string> { "Electric", "Steel" }),
        new Pokemon(82, "Magneton", new List<string> { "Electric", "Steel" }),
        new Pokemon(83, "Farfetch'd", new List<string> { "Normal", "Flying" }),
        new Pokemon(84, "Doduo", new List<string> { "Normal", "Flying" }),
        new Pokemon(85, "Dodrio", new List<string> { "Normal", "Flying" }),
        new Pokemon(86, "Seel", new List<string> { "Water" }),
        new Pokemon(87, "Dewgong", new List<string> { "Water", "Ice" }),
        new Pokemon(88, "Grimer", new List<string> { "Poison" }),
        new Pokemon(89, "Muk", new List<string> { "Poison" }),
        new Pokemon(90, "Shellder", new List<string> { "Water" }),
        new Pokemon(91, "Cloyster", new List<string> { "Water", "Ice" }),
        new Pokemon(92, "Gastly", new List<string> { "Ghost", "Poison" }),
        new Pokemon(93, "Haunter", new List<string> { "Ghost", "Poison" }),
        new Pokemon(94, "Gengar", new List<string> { "Ghost", "Poison" }),
        new Pokemon(95, "Onix", new List<string> { "Rock", "Ground" }),
        new Pokemon(96, "Drowzee", new List<string> { "Psychic" }),
        new Pokemon(97, "Hypno", new List<string> { "Psychic" }),
        new Pokemon(98, "Krabby", new List<string> { "Water" }),
        new Pokemon(99, "Kingler", new List<string> { "Water" }),
        new Pokemon(100, "Voltorb", new List<string> { "Electric" }),
        new Pokemon(101, "Electrode", new List<string> { "Electric" }),
        new Pokemon(102, "Exeggcute", new List<string> { "Grass", "Psychic" }),
        new Pokemon(103, "Exeggutor", new List<string> { "Grass", "Psychic" }),
        new Pokemon(104, "Cubone", new List<string> { "Ground" }),
        new Pokemon(105, "Marowak", new List<string> { "Ground" }),
        new Pokemon(106, "Hitmonlee", new List<string> { "Fighting" }),
        new Pokemon(107, "Hitmonchan", new List<string> { "Fighting" }),
        new Pokemon(108, "Lickitung", new List<string> { "Normal" }),
        new Pokemon(109, "Koffing", new List<string> { "Poison" }),
        new Pokemon(110, "Weezing", new List<string> { "Poison" }),
        new Pokemon(111, "Rhyhorn", new List<string> { "Ground", "Rock" }),
        new Pokemon(112, "Rhydon", new List<string> { "Ground", "Rock" }),
        new Pokemon(113, "Chansey", new List<string> { "Normal" }),
        new Pokemon(114, "Tangela", new List<string> { "Grass" }),
        new Pokemon(115, "Kangaskhan", new List<string> { "Normal" }),
        new Pokemon(116, "Horsea", new List<string> { "Water" }),
        new Pokemon(117, "Seadra", new List<string> { "Water" }),
        new Pokemon(118, "Goldeen", new List<string> { "Water" }),
        new Pokemon(119, "Seaking", new List<string> { "Water" }),
        new Pokemon(120, "Staryu", new List<string> { "Water" }),
        new Pokemon(121, "Starmie", new List<string> { "Water", "Psychic" }),
        new Pokemon(122, "Mr. Mime", new List<string> { "Psychic", "Fairy" }),
        new Pokemon(123, "Scyther", new List<string> { "Bug", "Flying" }),
        new Pokemon(124, "Jynx", new List<string> { "Ice", "Psychic" }),
        new Pokemon(125, "Electabuzz", new List<string> { "Electric" }),
        new Pokemon(126, "Magmar", new List<string> { "Fire" }),
        new Pokemon(127, "Pinsir", new List<string> { "Bug" }),
        new Pokemon(128, "Tauros", new List<string> { "Normal" }),
        new Pokemon(129, "Magikarp", new List<string> { "Water" }),
        new Pokemon(130, "Gyarados", new List<string> { "Water", "Flying" }),
        new Pokemon(131, "Lapras", new List<string> { "Water", "Ice" }),
        new Pokemon(132, "Ditto", new List<string> { "Normal" }),
        new Pokemon(133, "Eevee", new List<string> { "Normal" }),
        new Pokemon(134, "Vaporeon", new List<string> { "Water" }),
        new Pokemon(135, "Jolteon", new List<string> { "Electric" }),
        new Pokemon(136, "Flareon", new List<string> { "Fire" }),
        new Pokemon(137, "Porygon", new List<string> { "Normal" }),
        new Pokemon(138, "Omanyte", new List<string> { "Rock", "Water" }),
        new Pokemon(139, "Omastar", new List<string> { "Rock", "Water" }),
        new Pokemon(140, "Kabuto", new List<string> { "Rock", "Water" }),
        new Pokemon(141, "Kabutops", new List<string> { "Rock", "Water" }),
        new Pokemon(142, "Aerodactyl", new List<string> { "Rock", "Flying" }),
        new Pokemon(143, "Snorlax", new List<string> { "Normal" }),
        new Pokemon(144, "Articuno", new List<string> { "Ice", "Flying" }),
        new Pokemon(145, "Zapdos", new List<string> { "Electric", "Flying" }),
        new Pokemon(146, "Moltres", new List<string> { "Fire", "Flying" }),
        new Pokemon(147, "Dratini", new List<string> { "Dragon" }),
        new Pokemon(148, "Dragonair", new List<string> { "Dragon" }),
        new Pokemon(149, "Dragonite", new List<string> { "Dragon", "Flying" }),
        new Pokemon(150, "Mewtwo", new List<string> { "Psychic" }),
        new Pokemon(151, "Mew", new List<string> { "Psychic" }),
    };

    private readonly List<Pokemon> _johto = new()
    {
        new Pokemon(152, "Chikorita", new List<string> { "Grass" }),
        new Pokemon(153, "Bayleef", new List<string> { "Grass" }),
        new Pokemon(154, "Meganium", new List<string> { "Grass" }),
        new Pokemon(155, "Cyndaquil", new List<string> { "Fire" }),
        new Pokemon(156, "Quilava", new List<string> { "Fire" }),
        new Pokemon(157, "Typhlosion", new List<string> { "Fire" }),
        new Pokemon(158, "Totodile", new List<string> { "Water" }),
        new Pokemon(159, "Croconaw", new List<string> { "Water" }),
        new Pokemon(160, "Feraligatr", new List<string> { "Water" }),
        new Pokemon(161, "Sentret", new List<string> { "Normal" }),
        new Pokemon(162, "Furret", new List<string> { "Normal" }),
        new Pokemon(163, "Hoothoot", new List<string> { "Normal", "Flying" }),
        new Pokemon(164, "Noctowl", new List<string> { "Normal", "Flying" }),
        new Pokemon(165, "Ledyba", new List<string> { "Bug", "Flying" }),
        new Pokemon(166, "Ledian", new List<string> { "Bug", "Flying" }),
        new Pokemon(167, "Spinarak", new List<string> { "Bug", "Poison" }),
        new Pokemon(168, "Ariados", new List<string> { "Bug", "Poison" }),
        new Pokemon(169, "Crobat", new List<string> { "Poison", "Flying" }),
        new Pokemon(170, "Chinchou", new List<string> { "Water", "Electric" }),
        new Pokemon(171, "Lanturn", new List<string> { "Water", "Electric" }),
        new Pokemon(172, "Pichu", new List<string> { "Electric" }),
        new Pokemon(173, "Cleffa", new List<string> { "Fairy" }),
        new Pokemon(174, "Igglybuff", new List<string> { "Normal", "Fairy" }),
        new Pokemon(175, "Togepi", new List<string> { "Fairy" }),
        new Pokemon(176, "Togetic", new List<string> { "Fairy", "Flying" }),
        new Pokemon(177, "Natu", new List<string> { "Psychic", "Flying" }),
        new Pokemon(178, "Xatu", new List<string> { "Psychic", "Flying" }),
        new Pokemon(179, "Mareep", new List<string> { "Electric" }),
        new Pokemon(180, "Flaaffy", new List<string> { "Electric" }),
        new Pokemon(181, "Ampharos", new List<string> { "Electric" }),
        new Pokemon(182, "Bellossom", new List<string> { "Grass" }),
        new Pokemon(183, "Marill", new List<string> { "Water", "Fairy" }),
        new Pokemon(184, "Azumarill", new List<string> { "Water", "Fairy" }),
        new Pokemon(185, "Sudowoodo", new List<string> { "Rock" }),
        new Pokemon(186, "Politoed", new List<string> { "Water" }),
        new Pokemon(187, "Hoppip", new List<string> { "Grass", "Flying" }),
        new Pokemon(188, "Skiploom", new List<string> { "Grass", "Flying" }),
        new Pokemon(189, "Jumpluff", new List<string> { "Grass", "Flying" }),
        new Pokemon(190, "Aipom", new List<string> { "Normal" }),
        new Pokemon(191, "Sunkern", new List<string> { "Grass" }),
        new Pokemon(192, "Sunflora", new List<string> { "Grass" }),
        new Pokemon(193, "Yanma", new List<string> { "Bug", "Flying" }),
        new Pokemon(194, "Wooper", new List<string> { "Water", "Ground" }),
        new Pokemon(195, "Quagsire", new List<string> { "Water", "Ground" }),
        new Pokemon(196, "Espeon", new List<string> { "Psychic" }),
        new Pokemon(197, "Umbreon", new List<string> { "Dark" }),
        new Pokemon(198, "Murkrow", new List<string> { "Dark", "Flying" }),
        new Pokemon(199, "Slowking", new List<string> { "Water", "Psychic" }),
        new Pokemon(200, "Misdreavus", new List<string> { "Ghost" }),
        new Pokemon(201, "Unown", new List<string> { "Psychic" }),
        new Pokemon(202, "Wobbuffet", new List<string> { "Psychic" }),
        new Pokemon(203, "Girafarig", new List<string> { "Normal", "Psychic" }),
        new Pokemon(204, "Pineco", new List<string> { "Bug" }),
        new Pokemon(205, "Forretress", new List<string> { "Bug", "Steel" }),
        new Pokemon(206, "Dunsparce", new List<string> { "Normal" }),
        new Pokemon(207, "Gligar", new List<string> { "Ground", "Flying" }),
        new Pokemon(208, "Steelix", new List<string> { "Steel", "Ground" }),
        new Pokemon(209, "Snubbull", new List<string> { "Fairy" }),
        new Pokemon(210, "Granbull", new List<string> { "Fairy" }),
        new Pokemon(211, "Qwilfish", new List<string> { "Water", "Poison" }),
        new Pokemon(212, "Scizor", new List<string> { "Bug", "Steel" }),
        new Pokemon(213, "Shuckle", new List<string> { "Bug", "Rock" }),
        new Pokemon(214, "Heracross", new List<string> { "Bug", "Fighting" }),
        new Pokemon(215, "Sneasel", new List<string> { "Dark", "Ice" }),
        new Pokemon(216, "Teddiursa", new List<string> { "Normal" }),
        new Pokemon(217, "Ursaring", new List<string> { "Normal" }),
        new Pokemon(218, "Slugma", new List<string> { "Fire" }),
        new Pokemon(219, "Magcargo", new List<string> { "Fire", "Rock" }),
        new Pokemon(220, "Swinub", new List<string> { "Ice", "Ground" }),
        new Pokemon(221, "Piloswine", new List<string> { "Ice", "Ground" }),
        new Pokemon(222, "Corsola", new List<string> { "Water", "Rock" }),
        new Pokemon(223, "Remoraid", new List<string> { "Water" }),
        new Pokemon(224, "Octillery", new List<string> { "Water" }),
        new Pokemon(225, "Delibird", new List<string> { "Ice", "Flying" }),
        new Pokemon(226, "Mantine", new List<string> { "Water", "Flying" }),
        new Pokemon(227, "Skarmory", new List<string> { "Steel", "Flying" }),
        new Pokemon(228, "Houndour", new List<string> { "Dark", "Fire" }),
        new Pokemon(229, "Houndoom", new List<string> { "Dark", "Fire" }),
        new Pokemon(230, "Kingdra", new List<string> { "Water", "Dragon" }),
        new Pokemon(231, "Phanpy", new List<string> { "Ground" }),
        new Pokemon(232, "Donphan", new List<string> { "Ground" }),
        new Pokemon(233, "Porygon2", new List<string> { "Normal" }),
        new Pokemon(234, "Stantler", new List<string> { "Normal" }),
        new Pokemon(235, "Smeargle", new List<string> { "Normal" }),
        new Pokemon(236, "Tyrogue", new List<string> { "Fighting" }),
        new Pokemon(237, "Hitmontop", new List<string> { "Fighting" }),
        new Pokemon(238, "Smoochum", new List<string> { "Ice", "Psychic" }),
        new Pokemon(239, "Elekid", new List<string> { "Electric" }),
        new Pokemon(240, "Magby", new List<string> { "Fire" }),
        new Pokemon(241, "Miltank", new List<string> { "Normal" }),
        new Pokemon(242, "Blissey", new List<string> { "Normal" }),
        new Pokemon(243, "Raikou", new List<string> { "Electric" }),
        new Pokemon(244, "Entei", new List<string> { "Fire" }),
        new Pokemon(245, "Suicune", new List<string> { "Water" }),
        new Pokemon(246, "Larvitar", new List<string> { "Rock", "Ground" }),
        new Pokemon(247, "Pupitar", new List<string> { "Rock", "Ground" }),
        new Pokemon(248, "Tyranitar", new List<string> { "Rock", "Dark" }),
        new Pokemon(249, "Lugia", new List<string> { "Psychic", "Flying" }),
        new Pokemon(250, "Ho-Oh", new List<string> { "Fire", "Flying" }),
        new Pokemon(251, "Celebi", new List<string> { "Psychic", "Grass" }),
    };

    private readonly List<Pokemon> _hoenn = new()
    {
        new Pokemon(252, "Treecko", new List<string> { "Grass" }),
        new Pokemon(253, "Grovyle", new List<string> { "Grass" }),
        new Pokemon(254, "Sceptile", new List<string> { "Grass" }),
        new Pokemon(255, "Torchic", new List<string> { "Fire" }),
        new Pokemon(256, "Combusken", new List<string> { "Fire", "Fighting" }),
        new Pokemon(257, "Blaziken", new List<string> { "Fire", "Fighting" }),
        new Pokemon(258, "Mudkip", new List<string> { "Water" }),
        new Pokemon(259, "Marshtomp", new List<string> { "Water", "Ground" }),
        new Pokemon(260, "Swampert", new List<string> { "Water", "Ground" }),
        new Pokemon(261, "Poochyena", new List<string> { "Dark" }),
        new Pokemon(262, "Mightyena", new List<string> { "Dark" }),
        new Pokemon(263, "Zigzagoon", new List<string> { "Normal" }),
        new Pokemon(264, "Linoone", new List<string> { "Normal" }),
        new Pokemon(265, "Wurmple", new List<string> { "Bug" }),
        new Pokemon(266, "Silcoon", new List<string> { "Bug" }),
        new Pokemon(267, "Beautifly", new List<string> { "Bug", "Flying" }),
        new Pokemon(268, "Cascoon", new List<string> { "Bug" }),
        new Pokemon(269, "Dustox", new List<string> { "Bug", "Poison" }),
        new Pokemon(270, "Lotad", new List<string> { "Water", "Grass" }),
        new Pokemon(271, "Lombre", new List<string> { "Water", "Grass" }),
        new Pokemon(272, "Ludicolo", new List<string> { "Water", "Grass" }),
        new Pokemon(273, "Seedot", new List<string> { "Grass" }),
        new Pokemon(274, "Nuzleaf", new List<string> { "Grass", "Dark" }),
        new Pokemon(275, "Shiftry", new List<string> { "Grass", "Dark" }),
        new Pokemon(276, "Taillow", new List<string> { "Normal", "Flying" }),
        new Pokemon(277, "Swellow", new List<string> { "Normal", "Flying" }),
        new Pokemon(278, "Wingull", new List<string> { "Water", "Flying" }),
        new Pokemon(279, "Pelipper", new List<string> { "Water", "Flying" }),
        new Pokemon(280, "Ralts", new List<string> { "Psychic", "Fairy" }),
        new Pokemon(281, "Kirlia", new List<string> { "Psychic", "Fairy" }),
        new Pokemon(282, "Gardevoir", new List<string> { "Psychic", "Fairy" }),
        new Pokemon(283, "Surskit", new List<string> { "Bug", "Water" }),
        new Pokemon(284, "Masquerain", new List<string> { "Bug", "Flying" }),
        new Pokemon(285, "Shroomish", new List<string> { "Grass" }),
        new Pokemon(286, "Breloom", new List<string> { "Grass", "Fighting" }),
        new Pokemon(287, "Slakoth", new List<string> { "Normal" }),
        new Pokemon(288, "Vigoroth", new List<string> { "Normal" }),
        new Pokemon(289, "Slaking", new List<string> { "Normal" }),
        new Pokemon(290, "Nincada", new List<string> { "Bug", "Ground" }),
        new Pokemon(291, "Ninjask", new List<string> { "Bug", "Flying" }),
        new Pokemon(292, "Shedinja", new List<string> { "Bug", "Ghost" }),
        new Pokemon(293, "Whismur", new List<string> { "Normal" }),
        new Pokemon(294, "Loudred", new List<string> { "Normal" }),
        new Pokemon(295, "Exploud", new List<string> { "Normal" }),
        new Pokemon(296, "Makuhita", new List<string> { "Fighting" }),
        new Pokemon(297, "Hariyama", new List<string> { "Fighting" }),
        new Pokemon(298, "Azurill", new List<string> { "Normal", "Fairy" }),
        new Pokemon(299, "Nosepass", new List<string> { "Rock" }),
        new Pokemon(300, "Skitty", new List<string> { "Normal" }),
        new Pokemon(301, "Delcatty", new List<string> { "Normal" }),
        new Pokemon(302, "Sableye", new List<string> { "Dark", "Ghost" }),
        new Pokemon(303, "Mawile", new List<string> { "Steel", "Fairy" }),
        new Pokemon(304, "Aron", new List<string> { "Steel", "Rock" }),
        new Pokemon(305, "Lairon", new List<string> { "Steel", "Rock" }),
        new Pokemon(306, "Aggron", new List<string> { "Steel", "Rock" }),
        new Pokemon(307, "Meditite", new List<string> { "Fighting", "Psychic" }),
        new Pokemon(308, "Medicham", new List<string> { "Fighting", "Psychic" }),
        new Pokemon(309, "Electrike", new List<string> { "Electric" }),
        new Pokemon(310, "Manectric", new List<string> { "Electric" }),
        new Pokemon(311, "Plusle", new List<string> { "Electric" }),
        new Pokemon(312, "Minun", new List<string> { "Electric" }),
        new Pokemon(313, "Volbeat", new List<string> { "Bug" }),
        new Pokemon(314, "Illumise", new List<string> { "Bug" }),
        new Pokemon(315, "Roselia", new List<string> { "Grass", "Poison" }),
        new Pokemon(316, "Gulpin", new List<string> { "Poison" }),
        new Pokemon(317, "Swalot", new List<string> { "Poison" }),
        new Pokemon(318, "Carvanha", new List<string> { "Water", "Dark" }),
        new Pokemon(319, "Sharpedo", new List<string> { "Water", "Dark" }),
        new Pokemon(320, "Wailmer", new List<string> { "Water" }),
        new Pokemon(321, "Wailord", new List<string> { "Water" }),
        new Pokemon(322, "Numel", new List<string> { "Fire", "Ground" }),
        new Pokemon(323, "Camerupt", new List<string> { "Fire", "Ground" }),
        new Pokemon(324, "Torkoal", new List<string> { "Fire" }),
        new Pokemon(325, "Spoink", new List<string> { "Psychic" }),
        new Pokemon(326, "Grumpig", new List<string> { "Psychic" }),
        new Pokemon(327, "Spinda", new List<string> { "Normal" }),
        new Pokemon(328, "Trapinch", new List<string> { "Ground" }),
        new Pokemon(329, "Vibrava", new List<string> { "Ground", "Dragon" }),
        new Pokemon(330, "Flygon", new List<string> { "Ground", "Dragon" }),
        new Pokemon(331, "Cacnea", new List<string> { "Grass" }),
        new Pokemon(332, "Cacturne", new List<string> { "Grass", "Dark" }),
        new Pokemon(333, "Swablu", new List<string> { "Normal", "Flying" }),
        new Pokemon(334, "Altaria", new List<string> { "Dragon", "Flying" }),
        new Pokemon(335, "Zangoose", new List<string> { "Normal" }),
        new Pokemon(336, "Seviper", new List<string> { "Poison" }),
        new Pokemon(337, "Lunatone", new List<string> { "Rock", "Psychic" }),
        new Pokemon(338, "Solrock", new List<string> { "Rock", "Psychic" }),
        new Pokemon(339, "Barboach", new List<string> { "Water", "Ground" }),
        new Pokemon(340, "Whiscash", new List<string> { "Water", "Ground" }),
        new Pokemon(341, "Corphish", new List<string> { "Water" }),
        new Pokemon(342, "Crawdaunt", new List<string> { "Water", "Dark" }),
        new Pokemon(343, "Baltoy", new List<string> { "Ground", "Psychic" }),
        new Pokemon(344, "Claydol", new List<string> { "Ground", "Psychic" }),
        new Pokemon(345, "Lileep", new List<string> { "Rock", "Grass" }),
        new Pokemon(346, "Cradily", new List<string> { "Rock", "Grass" }),
        new Pokemon(347, "Anorith", new List<string> { "Rock", "Bug" }),
        new Pokemon(348, "Armaldo", new List<string> { "Rock", "Bug" }),
        new Pokemon(349, "Feebas", new List<string> { "Water" }),
        new Pokemon(350, "Milotic", new List<string> { "Water" }),
        new Pokemon(351, "Castform", new List<string> { "Normal" }),
        new Pokemon(352, "Kecleon", new List<string> { "Normal" }),
        new Pokemon(353, "Shuppet", new List<string> { "Ghost" }),
        new Pokemon(354, "Banette", new List<string> { "Ghost" }),
        new Pokemon(355, "Duskull", new List<string> { "Ghost" }),
        new Pokemon(356, "Dusclops", new List<string> { "Ghost" }),
        new Pokemon(357, "Tropius", new List<string> { "Grass", "Flying" }),
        new Pokemon(358, "Chimecho", new List<string> { "Psychic" }),
        new Pokemon(359, "Absol", new List<string> { "Dark" }),
        new Pokemon(360, "Wynaut", new List<string> { "Psychic" }),
        new Pokemon(361, "Snorunt", new List<string> { "Ice" }),
        new Pokemon(362, "Glalie", new List<string> { "Ice" }),
        new Pokemon(363, "Spheal", new List<string> { "Ice", "Water" }),
        new Pokemon(364, "Sealeo", new List<string> { "Ice", "Water" }),
        new Pokemon(365, "Walrein", new List<string> { "Ice", "Water" }),
        new Pokemon(366, "Clamperl", new List<string> { "Water" }),
        new Pokemon(367, "Huntail", new List<string> { "Water" }),
        new Pokemon(368, "Gorebyss", new List<string> { "Water" }),
        new Pokemon(369, "Relicanth", new List<string> { "Water", "Rock" }),
        new Pokemon(370, "Luvdisc", new List<string> { "Water" }),
        new Pokemon(371, "Bagon", new List<string> { "Dragon" }),
        new Pokemon(372, "Shelgon", new List<string> { "Dragon" }),
        new Pokemon(373, "Salamence", new List<string> { "Dragon", "Flying" }),
        new Pokemon(374, "Beldum", new List<string> { "Steel", "Psychic" }),
        new Pokemon(375, "Metang", new List<string> { "Steel", "Psychic" }),
        new Pokemon(376, "Metagross", new List<string> { "Steel", "Psychic" }),
        new Pokemon(377, "Regirock", new List<string> { "Rock" }),
        new Pokemon(378, "Regice", new List<string> { "Ice" }),
        new Pokemon(379, "Registeel", new List<string> { "Steel" }),
        new Pokemon(380, "Latias", new List<string> { "Dragon", "Psychic" }),
        new Pokemon(381, "Latios", new List<string> { "Dragon", "Psychic" }),
        new Pokemon(382, "Kyogre", new List<string> { "Water" }),
        new Pokemon(383, "Groudon", new List<string> { "Ground" }),
        new Pokemon(384, "Rayquaza", new List<string> { "Dragon", "Flying" }),
        new Pokemon(385, "Jirachi", new List<string> { "Steel", "Psychic" }),
        new Pokemon(386, "Deoxys", new List<string> { "Psychic" }),
    };

    public PokedexExtensionPage()
    {
        Icon = new(string.Empty);
        Name = "Pokedex";
    }

    public override IListItem[] GetItems()
    {
        return _kanto.AsEnumerable().Concat(_johto.AsEnumerable()).Concat(_hoenn.AsEnumerable()).Select(p => GetPokemonListItem(p)).ToArray();
        /*return [
            new ListSection()
            {
                Title = "Kanto",
                Items = _kanto
                    .AsEnumerable()
                    .Select(p => GetPokemonListItem(p)).ToArray(),
            },
            new ListSection()
            {
                Title = "Johto",
                Items = _johto
                    .AsEnumerable()
                    .Select(p => GetPokemonListItem(p)).ToArray(),
            },
            new ListSection()
            {
                Title = "Hoenn",
                Items = _hoenn
                    .AsEnumerable()
                    .Select(p => GetPokemonListItem(p)).ToArray(),
            },
        ];*/
    }

    private static ListItem GetPokemonListItem(Pokemon pokemon)
    {
        return new ListItem(new PokemonPage(pokemon))
        {
            Subtitle = $"#{pokemon.Number}",
            Tags = pokemon.Types.Select(t => new Tag() { Text = t, Color = GetColorForType(t) }).ToArray(),
        };
    }

    // Dictionary mapping Pokémon types to their corresponding colors
    private static readonly Dictionary<string, Color> TypeColors = new()
    {
        { "Normal", Color.FromArgb(255, 168, 168, 120) },   // Light Brownish Grey
        { "Fire", Color.FromArgb(255, 240, 128, 48) },      // Orange-Red
        { "Water", Color.FromArgb(255, 104, 144, 240) },    // Medium Blue
        { "Electric", Color.FromArgb(255, 248, 208, 48) },  // Yellow
        { "Grass", Color.FromArgb(255, 120, 200, 80) },     // Green
        { "Ice", Color.FromArgb(255, 152, 216, 216) },      // Cyan
        { "Fighting", Color.FromArgb(255, 192, 48, 40) },   // Red
        { "Poison", Color.FromArgb(255, 160, 64, 160) },    // Purple
        { "Ground", Color.FromArgb(255, 224, 192, 104) },   // Yellowish Brown
        { "Flying", Color.FromArgb(255, 168, 144, 240) },   // Light Blue
        { "Psychic", Color.FromArgb(255, 248, 88, 136) },   // Pink
        { "Bug", Color.FromArgb(255, 168, 184, 32) },       // Greenish Yellow
        { "Rock", Color.FromArgb(255, 184, 160, 56) },      // Brown
        { "Ghost", Color.FromArgb(255, 112, 88, 152) },     // Dark Purple
        { "Dragon", Color.FromArgb(255, 112, 56, 248) },    // Blue-Violet
        { "Dark", Color.FromArgb(255, 112, 88, 72) },       // Dark Brown
        { "Steel", Color.FromArgb(255, 184, 184, 208) },    // Light Grey
        { "Fairy", Color.FromArgb(255, 238, 153, 172) },    // Light Pink
    };

    // Method to get the color for a given type
    public static Color GetColorForType(string type)
    {
        // Check if the type exists in the dictionary
        if (TypeColors.TryGetValue(type, out var color))
        {
            return color;
        }

        // Default color (e.g., white) if the type is not found
        return Color.FromArgb(255, 255, 255, 255);
    }
}
