# Source of Truth
When implementing gameplay features, use the following priority:
- Explicit instructions from the user
- Game design documents
- Existing code conventions
- Existing implementation
If two sources conflict and the correct behavior is unclear, do not make assumptions that significantly change gameplay. Report the conflict and ask for clarification.

# Modification Scope
- AI assistants are allowed to modify code files only.
- Do not modify Unity assets, scenes, prefabs, materials, animations, textures, audio files, or other project content.

# Code Architecture and Layout
- All Monobehaviour scripts should follow this order:
    + Constants
    + Static fields
    + Serialized fields
    + Private fields
    + Public properties
    + Events
    + Methods
- Use // ===== Region's name ===== // to split between regions. For example: // ===== Constants =====//.

# Naming Conventions
- Enum: should be singular.
- Interface: always prefix with "I"
- ScriptableObject: suffix with Config, Data, Settings, Database, or Profile.
- Class: should be named after their primary responsibility. Prefer nouns or noun phrases.
- Generic Type: should use T prefix.
- Constant Fields: use UPPER_SNAKE_CASE.
- Private Fields: use camelCase with underscore prefix.
- Boolean Fields: should read naturally as yes/no questions. Prefer prefixes: is, has, can, should, or was.
- Coroutine Fields: suffix with "Routine".
- Static Readonly Fields: use UPPER_SNAKE_CASE.
- Properties: use PascalCase.
- Events: use past tense with "On" prefix.
- Methods: use PascalCase and start with a verb whenever possible.
- Coroutine Methods: prefix with "Co_".
- Local Variables: use camelCase.

# Code Documenations
- Use English for code documentations.
- Write self-explanatory code whenever possible.
- Do not add comments that merely restate what the code does.
- Prefer meaningful variable, method, and class names over excessive comments.
- Add comments only when they provide context, reasoning, assumptions, or explain non-obvious behavior.