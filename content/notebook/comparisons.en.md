# Comparisons: greater, less, equal

A comparison answers yes or no: the result is a `bool`.

| Operator | Question | True, for example |
| --- | --- | --- |
| `==` | equal? | `5 == 5` |
| `!=` | different? | `5 != 3` |
| `<` | less? | `3 < 5` |
| `>` | greater? | `5 > 3` |
| `<=` | less or equal? | `5 <= 5` |
| `>=` | greater or equal? | `6 >= 5` |

`=` stores a value and `==` compares. `if (hour = 4)` does not compile in C#: the right one is `if (hour == 4)`.

Mind the edges. "From 10 pm on" is `hour >= 22`, and "before 4 am" is `hour < 4`. A test at the exact edge hour shows which of the two you wrote.

Texts are compared with `==` too: `password == "abc"` is only true with the same letters, in the same case.
