# Extensibles, The BepInEx Hook Extension Library

HookGenExtender, or "Extensibles", is an experimental productivity toolkit designed to layer on top of BepInEx's built-in Hooks library. It is closely inspired by the underlying behavior of Sponge's [Mixin](https://github.com/SpongePowered/Mixin) for Minecraft. The behavior of Mixins requires hook classes to be instances (rather than static, like BIE hooks), and just before startup, the bytecode of a modder's mixin class is merged in with that of the class they are modding. The result is that the mixin class gets to behave as if it is an instance of the class it is injecting into because, for all intents and purposes, it *is* that class.

This behavior is extremely useful for games like *Rain World* (which is what this was designed for originally), which notoriously *avoids* object inheritence for variations of the same class, making patches often quite annoying and data storage for custom objects even worse, heavily relying on `ConditionalWeakTable`.

Of course, this isn't working in Java, so there's no fancy classloader hacks and abuses of Java's lack of compile-time interface cast checking going on here.

# Okay, so what does it do? How do I use it?

This tool creates the `Extensible` namespace (comparable to BIE's `On` namespace), which contains its own version of all classes from the base game. These Extensible classes declare all fields (as properties that use get/set to read/write the original field), properties, and methods of the original class virtually.

To use it, make a new class that extends the extensible counterpart (i.e. `class MyCoolCustomPlayer : Extensible.Player`). Override any methods or properties that you wish to replace here.

Every method has a hook redirector behind the scenes. This technique is extremely powerful, and changes the behavior of the method depending on when it is being called.
* If it is called by a hook, calling `base.Method()` is identical to calling `orig(self)` in a traditional BIE hook.
* If it is called by you manually, calling `base.Method()` is identical to calling `Original.Method()`, and will fire hooks.
  * It will *not* re-run your method, the hook system will skip yours to prevent re-entry.

# Example implementation

In Rain World, this is what a typical setup might look like:

**Class:** `MyPlayer.cs`
```cs
public class MyPlayer : Extensible.Player {

	private bool _gotPermissionToDie = false;

	// Call this from the mod's Awake()/OnEnable()
	internal static void Initialize() {
		On.Player.ctor += (originalMethod, @this, abstractCreature, world) => {
			Binder<MyPlayer>.Bind(@this); // This is where the magic happens.
			originalMethod(@this, abstractCreature, world);
		};
		On.Player.Destroy += (originalMethod, @this) => {
			bool unbound = Binder<MyPlayer>.TryReleaseCurrentBinding(@this);
		};
	}
	
	public override void Die() {
		if (!_gotPermissionToDie) return;
		base.Die();
	}

}
```
