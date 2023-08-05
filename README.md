# HookGenExtender, The BepInEx Hook Extension Library

HookGenExtender is, or "Extensibles" are, an experimental toolkit designed to layer on top of BepInEx's built-in Hooks library that gets generated on startup. It is closely inspired by the underlying behavior of Sponge's [Mixin](https://github.com/SpongePowered/Mixin) for Minecraft. The behavior of Mixins requires hook classes to be instances (rather than static, like BIE hooks), and just before startup, the bytecode of a modder's mixin class is merged in with that of the class they are modding. The result is that the mixin class gets to behave as if it is an instance of the class it is injecting into because, for all intents and purposes, it *is* that class.

This behavior is extremely useful for games like *Rain World* (which is what this was designed for originally), which notoriously *avoids* object inheritence for variations of the same class, making patches often quite annoying and data storage for custom objects even worse, heavily relying on `ConditionalWeakTable`.

# Okay, so what does it do? How do I use it?

HookGenExtender creates the `Extensible` namespace (comparable to BIE's `On` namespace), which contains its own version of all classes from the base game. These Extender classes (as they will be referred to from here onward) declare all fields (as properties), properties, and methods of the original class virtually.

When a modder wants to design a custom object that behaves like the object they are injecting into, Extender classes should be used in place of replacing the instance that the game stores (i.e. a custom `Player` class in Rain World). Despite this fact, **you write your code just like you would write it if you were extending the class!** All methods are called by hooks and will execute just as you might expect an override to.

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
			Binder<MechPlayer>.Bind(@this); // This is where the magic happens.
			originalMethod(@this, abstractCreature, world);
		};
	}
	
	public override void Die() {
		if (!_gotPermissionToDie) return;
		base.Die();
	}

}
```
