# Designing a resource-safe C# component | Part 1/4

A file importer should be easy to change without becoming easy to break.

Our example reads product rows from a file, validates them, and saves them. Resource-safe means it releases the files it owns, including when work fails.

14\. Interface or abstract base class?

An interface defines a contract: operations a type promises to provide. An abstract base class can also share implementation and state with derived classes.

Scenario: the importer accepts `IProductRowParser`, with a `Parse(line)` operation. One implementation reads a supplier's product format; another can support a different supplier. The importer needs the contract, not a common parent with shared internal state.

Choose a base class when related types genuinely need shared behavior and rules. Do not introduce one merely because two classes contain similar code: future changes to the base can affect every derived class.

15\. Composition or inheritance?

Composition means building behavior from collaborating objects. Inheritance means extending a base type and taking on its contract.

Our importer receives a parser and a writer through its constructor:

`new ProductImporter(parser, writer)`

We can change the file format without changing storage, or change storage without changing parsing.

An `SqlImporter` subclass and a `PostgresImporter` subclass become awkward when each must also support several file formats. Composition avoids creating a subclass for every combination.

Inheritance still fits a genuine "is-a" relationship where the derived type can honor the base contract.

16\. What does single responsibility mean here?

The Single Responsibility Principle says a component should have one cohesive reason to change, not necessarily one method.

The parser changes when the supplier's row format changes. The writer changes when storage changes. The importer coordinates reading, validation, and saving.

Practical rule: keep SQL commands out of the parser and column-splitting rules out of the writer.

Do not create a separate class for every line of code. Split responsibilities where requirements vary independently.

Our sample also rejects malformed rows before saving anything. That is a deliberate import policy, not something design principles decide for us.

Next: extending the importer without breaking its contracts.

Detailed scenarios and runnable C# example, sections 014-016:
https://github.com/danielbrazil/linkedin-content-plan/blob/main/CSharp_DotNet_Resource_Safe_Components.md

#CSharp #DotNet #SoftwareDesign
