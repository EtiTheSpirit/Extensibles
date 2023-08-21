# Preliminary Warning

Extensibles is in a *testing phase*. It has not been widely tested outside of relatively niche scenarios and *may have unexpected behavior.* Until more extensive testing is done, **use Extensibles at your own risk.** There might be some nasty bugs that I simply didn't or couldn't foresee that brick compatibility with other mods or cause otherwise unpredictable errors.

In general, **it is NOT recommended to use this for production mods at this time. Play with it and see if it works for you instead.**


# Extensibles, The BepInEx Hook Extension Library

Extensibles is an experimental productivity toolkit designed to layer on top of BepInEx's built-in Hooks library. 

It is closely inspired by the underlying behavior of Sponge's [Mixin](https://github.com/SpongePowered/Mixin) for *Minecraft*, which uses instance-based injections that meld bytecode together between the original class and the mixin class, allowing the mixin to behave as if it were an instance of the original type.

This Mixin-like behavior is extremely useful for games like *Rain World* (which is what this was designed for originally), which notoriously *avoids* object inheritence for variations of the same class. This makes patches quite annoying to work with, and custom data storage very tedious. An example of this is its `Player` class, which has *all* campaign-unique behaviors for *every playable character in the game* smashed into it.

Extensibles aims to remedy some of the pain of this odd design by allowing you to write your classes as if you are that class.

# Why?
Well, to be frank... Extensibles *is* just a comically thick boilerplate. The leverage it has over stock hooks varies wildly across games, but in general...
* Extensibles makes working with discrete objects feel more natural, as access (via hooks) is no longer static.
	* This might benefit newer modders in some ways, but harm them in others (i.e. its good to understand hooks).
	* This can also help code cleanlines, especially in games like Rain World that have cluttered types.
	* This might be more familiar to modders coming from other games, especially Minecraft due to its similarities with Mixin.
* Extensibles allows automatically hooking properties with this technique (though doing this normally *is* just one line of code).

Overall, the only reason you should choose to use this over any other solution comes down to preference. If the presence of one more dependency and some boilerplate is worth it for convenience, then enjoy, otherwise carry on with your work like normal and pay no mind to this toolkit.

# Limitations
- **Extensibles is NOT thread safe, and only operates predictably in a single-threaded environment.**
	- This may be changed in the future, but no promises.
- **Only members of the `On` namespace become Extensible types.**
	- This simplifies the code pretty dramatically for my generator tool as I can piggyback off of that namespace to filter my types out.
- Extensibles cannot detect construction of original counterparts for automatic binding. 
	- Whether or not this is a good idea is debatable as every automagic feature makes it harder to debug and diagnose issues caused by this module; it creates a purposeful break or boundary in the code flow.
- Extensibles cannot extend finalizers (but it *can* extend a `Dispose` method, if present).
	- Extensibles does not extend constructors either, but a unique `Bind` method is generated for each constructor, allowing you to *mimic* original constructors instead.
	- Again, the base extensible constructor *has no logic*. This is why you must hook into the original constructor (in the `Initialize` method, as seen in the example) and explicitly call the `Bind` method, and is also why you should not be manually invoking constructors.
- Extensibles does not override methods with generic type parameters.
	- This could probably be done later on, but for now, BIE doesn't do it so I won't either.


## How does it work?

This tool creates the `Extensible` namespace (comparable to BIE's `On` namespace), which contains its own version of all classes from the base game. 

Each class contains "mirrored" members that follow these rules:
* All fields are exposed as `ref` properties. This way you can read/write to them normally, but also make `ref`s to them as if they are fields.
	* `readonly` fields remain `readonly`, in the form of standard (non-`ref`) properties without a setter.
* All methods are mirrored in a wrapper that adds behavior to the `base.Method()` call. **This is where Extensibles's most complex behavior comes in, so this might be difficult to keep track of.**
	* Methods are simultaneously proxies to the original class *and* BIE hooks that are automatically subscribed!
	* If you *manually call your method in your extensible class*, calling `base.Method()` is identical to calling `Original.Method()` (the vanilla method) and *will invoke other hooks.*
		* Internally, Extensibles will *prevent your method from being re-entered.* Do *not* write code to handle a hook invoking your method while you are invoking it. This is already accounted for by the system.
	* If your method *is called by a hook* (that is, vanilla code was called by someone else), calling `base.Method()` is identical to calling `orig(self)` in a traditional BIE hook.
	* **The result of this behavior is that no matter where or when your method is called, it *always* behaves like you would expect a typical method call to behave, all while preserving compatibility with other mods.**
		* In general, this means that you should not worry about how hooks will behave. All of the complex behavior and what-ifs are handled by the system.
* Properties are mirrored just like methods, with the proxy/hook behavior being added to their getter and/or setter independently.
	* Properties are bound via `Hook`, thus they too will be compatible with any mods that use RuntimeDetour to hook into properties.

There are additional features as well:
* Extensible types can be implicitly cast to their original counterparts, including base types of the original counterpart.
* Original types can be explicitly cast into an extensible type to resolve an instance of said type, though if there is no binding it will raise `InvalidCastException`. This can be used in cases where you know for a fact that a binding *should* exist.
	* To avoid the exception, `Binder<T>` has a `TryGetBoundInstance` method that will try to return a binding for an original type.

# Example implementation

To use it, make a new class that extends the extensible counterpart (i.e. `class MyCoolCustomPlayer : Extensible.Player`). Override any methods or properties that you wish to replace here.

In Rain World, this is what a hypothetical setup might look like:

**Class:** `MyPlayer.cs`
```cs
// First note: You ***MUST*** seal your class. If it is unsealed, 
// the binder's initializer will raise an InvalidOperationException.
//
// When the Binder searches for methods to automatically hook, 
// it looks for *explicitly declared* members. 
//
// This means that if you make an abstract extensible class, inherited 
// virtual members **WILL NOT BE AUTOMATICALLY BOUND** unless you override 
// them and call the base method from the override.
public sealed class MyPlayer : Extensible.Player {

	private bool _gotPermissionToDie = false;

	MyPlayer(Player original, AbstractCreature creature, World world) : base(original) {
		// This constructor will be called by the binder (see below).
		
		// Note that this constructor *MUST* be private!
		// You can do this by having no access modifier (as done here) 
		// or explicitly putting private, up to you.

		// If the constructor is not private, the binder will raise an exception 
		// reminding you to do so (this is to relay the fact that you should not be
		// calling your ctors manually!)

		// Also note that the base call only accepts original - the base constructor 
		// doesn't actually do any logic from the original class, it just ensures that the
		// Original property (which all mirrors use) is set *before* your constructor executes.

		// You MUST declare a constructor like this to use its corresponding bind method! 
		// If this constructor was missing, and Bind(player, abstractCreature, world)
		// got called, the Binder would raise an exception reporting that this constructor 
		// was missing, thus meaning the bind method is not available.
	}

	// Call this from the mod's Awake()/OnEnable()
	internal static void Initialize() {
		On.Player.ctor += (originalMethod, @this, abstractCreature, world) => {
			originalMethod(@this, abstractCreature, world);
			if (@this.name == MyIDs.MyCharacter) {
				Binder<MyPlayer>.Bind(@this, abstractCreature, world); // This is where the magic happens.
				// Notice that the bind method matches the signature of the constructor hook. There is also a default variant of Bind (that only takes @this)
				// You shouldn't do any object initialization here, do that in your constructor instead.

				// Another note is that I only Bind to a class that I know is mine. This makes it more convenient to write code as I can skip out
				// on verifying that the hook is running on my class or on another. Of course, you should always try to invoke base behavior whenever
				// possible, lest you cause incompatibilities between mods.

				// NOTE: Only one binding can be made at a time for a specific instance of player. Multiple (different) players can be bound at once, but
				// the same (singular) player cannot. Attempting to do so will raise an exception.
			}
		};
		On.Player.Destroy += (originalMethod, @this) => {
			Binder<MyPlayer>.TryReleaseBinding(@this); // This can be used to manually dispose of a binding.
			// Most importantly, THIS IS *NOT* REQUIRED, but is recommended when possible.
			// By default, the Binder will free objects alongside garbage collection of the original type (@this), which works but has no guarantees.
		};
	}
	
	public override void Die() {
		if (!_gotPermissionToDie) return;
		base.Die();
	}

}
```