using Satchel.BetterMenus;

namespace GodhomeQoL.Modules.CollectorPhases;

public static class CollectorPhasesMenu {
	public static MenuScreen GetMenu(MenuScreen parent) {
		_ = ModuleManager.TryGetModule(typeof(CollectorPhases), out Module? module);

		List<Element> elements = [];

		elements.Add(Blueprints.HorizontalBoolOption(
			"Modules/CollectorPhases".Localize(),
			$"ToggleableLevel/{ToggleableLevel.ChangeScene}".Localize(),
			val => {
				if (module != null) {
					module.Enabled = val;
				}
			},
			() => module?.Enabled ?? false
		));

		elements.AddRange(CollectorPhases.MenuElements());

		return new Menu(
			"CollectorPhases".Localize(),
			[..elements]
		).GetMenuScreen(parent);
	}
}
