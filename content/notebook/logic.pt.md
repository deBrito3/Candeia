# E, ou, não: && || !

Para juntar perguntas numa só:

- `a && b` (e): verdadeiro só quando as duas são verdadeiras.
- `a || b` (ou): verdadeiro quando pelo menos uma é verdadeira.
- `!a` (não): inverte a resposta. `!true` é `false`.

```csharp
bool abreLoja = diaUtil && !feriado;
bool padariaAberta = hora >= 22 || hora < 4;
bool expediente = hora >= 8 && hora < 17;
```

Um intervalo que atravessa a meia-noite, como das 22h às 4h, usa `||`: nenhuma hora é ao mesmo tempo maior que 22 e menor que 4. Um intervalo dentro do dia, como das 8h às 17h, usa `&&`.

Trocar um pelo outro é um defeito comum, e o código ainda compila: o `&&` vira uma pergunta que nunca é verdadeira, ou o `||` vira uma que quase sempre é.
