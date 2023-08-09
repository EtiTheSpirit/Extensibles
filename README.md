# Extensibles, The BepInEx Hook Extension Library

### Note: This is still a TEST. Please see the bottom of this document for more information and limitations.

HookGenExtender, or "Extensibles", is an experimental productivity toolkit designed to layer on top of BepInEx's built-in Hooks library. It is closely inspired by the underlying behavior of Sponge's [Mixin](https://github.com/SpongePowered/Mixin) for *Minecraft*. The behavior of Mixins requires hook classes to be instances (rather than static, like BIE hooks), and just before startup, the bytecode of a modder's mixin class is merged in with that of the class they are modding. The result is that the mixin class gets melded into the original class, and thus it gets to behave as if it is an instance of the class it is injecting into; for all intents and purposes, it *is* that class.

This behavior is extremely useful for games like *Rain World* (which is what this was designed for originally), which notoriously *avoids* object inheritence for variations of the same class, making patches often quite annoying and data storage for custom objects even worse, heavily relying on `ConditionalWeakTable` (an example of this is its `Player` class, which has all campaign-unique behaviors for every character smashed into that single `Player` class).

Of course, this isn't working in Java, so there's no fancy classloader hacks and abuses of Java's lack of compile-time interface cast checking going on here. This system instead uses some clever, albeit messy, IL generation to (im)politely proxy calls through the three ring circus to allow writing pseudo-inheritence.

# Okay, so what does that actually mean? How do I use it?

This tool creates the `Extensible` namespace (comparable to BIE's `On` namespace), which contains its own version of all classes from the base game. These Extensible classes declare all fields (as properties that use get/set to read/write the original field), properties, and methods of the original class virtually.

To use it, make a new class that extends the extensible counterpart (i.e. `class MyCoolCustomPlayer : Extensible.Player`). Override any methods or properties that you wish to replace here.

The signature behavior that makes Extensibles so powerful is its **runtime hook redirector.** In simple terms...
* If the vanilla method (and thus your override) is called by a hook, calling `base.Method()` is identical to calling `orig(self)` in a traditional BIE hook.
* If the vanilla method (or your override) is called by you manually, calling `base.Method()` is identical to calling `Original.Method()`, and will fire hooks for BIE so that other mods have their chance to run as well.
  * Importantly, doing this will *not* re-run your method, the hook system will skip yours to prevent re-entry.

If this seems a bit confusing, the takeaway is that you write your code like you would expect to write an inherited class in any other program, and it just works(tm), including with other mods.

# Example implementation

In Rain World, this is what a hypothetical setup might look like:

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

# Limitations

Currently, Extensibles is **NOT** capable of the following:
- Property injections (you still need to do these the old fashioned way).
- Detecting when your extensible class needs to be constructed (as shown above, you need to hook `ctor` still).

# Warnings

Extensibles is in a *testing phase*. It has not been widely tested outside of relatively niche scenarios and *may have unexpected behavior.* Until more extensive testing is done, **use Extensibles at your own risk.** There might be some nasty bugs that I simply didn't foresee.

In general, **it is NOT recommended to use this for production mods at this time.**