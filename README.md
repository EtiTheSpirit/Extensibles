# HookGenExtender, The BepInEx Hook Extension Library

HookGenExtender is an experimental module designed to layer on top of BepInEx's build in Hooks library that gets generated on startup. It is closely inspired by the underlying behavior of Sponge's [Mixin](https://github.com/SpongePowered/Mixin) for Minecraft. The behavior of Mixins requires hook classes to be instances (rather than static, like BIE hooks), and just before startup, the bytecode of a modder's mixin class is merged in with that of the class they are modding. The result is that the mixin class gets to behave as if it is an instance of the class it is injecting into because, for all intents and purposes, it *is* that class.

This behavior is extremely useful for games like *Rain World* (which is what this was designed for originally), which notoriously *avoids* object inheritence for variations of the same class, making patches often quite annoying and data storage for custom objects even worse, heavily relying on `ConditionalWeakTable`.

# Okay, so what does it do? How do I use it?

HookGenExtender creates the `Extensible` namespace (comparable to BIE's `On` namespace), which contains its own version of all classes from the base game. These Extender classes (as they will be referred to from here onward) declare all fields (as properties), properties, and methods of the original class virtually.

When a modder wants to design a custom object that behaves like the object they are injecting into, Extender classes should be used in place of replacing the instance that the game stores (i.e. a custom `Player` class in Rain World).

In this class, calling any method has adaptive behavior:
- The method overrides themselves in the Extensible classes are hooks that are automatically bound based on which methods you override.
  - This means that your methods get called when the game hook runs.
- Yet despite this, if called *outside* of the context of a hook (i.e. by you), it will behave like calling the respective method of the original class, including the execution of hooks!
  - Of course, this raises the concern of "Wouldn't my method get executed twice?" to which I answer no. It will not.
	- The mechanism in which this is performed **is NOT thread-safe.**

# Example implementation

In Rain World, this is what a typical setup might look like:

**Class:** `MyModMain.cs`
```cs
public class MyModMain : BaseUnityPlugin {
	private void OnEnable() {
		On.Player.ctor += OnPlayerConstructing;
	}

	private static void OnPlayerConstructing(orig, self, ...) {
		orig(self, ...); // Using ... as a shorthand, of course.
		WeakReference<MyPlayer> myPlayer = Extensible.Player.Binder<MyPlayer>.Bind(self);
	}
}
```
**Class:** `MyPlayer.cs`
```cs
public class MyPlayer : Extensible.Player {

	private bool _gotPermissionToDie = false;
	
	public override void Die() {
		if (!_gotPermissionToDie) return;
		base.Die(); // And to reiterate:
		// If this method (override void Die()) was called by a hook, then base.Die() is identical to what would traditionally be orig(self) in a hook.
		// If this method (override void Die()) was called manually, then base.Die() is identical to calling Player.Die(), which triggers hooks.
		// And again, if the second of those two scenarios is what happens, this hook (override void Die()) will be skipped so it won't run over itself.
	}

}
```
