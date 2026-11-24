# Генерация POCO из JSON

За генерацию POCO отвечает `PocoGenerator`. Roslyn загружает его и `DuckCastGenerator` из одной сборки анализатора `DuckiDuckiCastingLib`.

## Подключение

Скачайте архив `DuckiDuckiCastingLib-nuget` из GitHub Actions, распакуйте его и добавьте папку с `.nupkg` как локальный источник NuGet. Подключите пакет `DuckiDuckiCastingLib` с версией из имени файла. Для пакета нужен компилятор Roslyn 4.14 или новее, например из .NET SDK 10.

```xml
<ItemGroup>
  <PackageReference Include="DuckiDuckiCastingLib" Version="0.1.0-ci.RUN_ID.RUN_ATTEMPT" PrivateAssets="all" />
  <AdditionalFiles Include="PocoJson/Stages/**/*.poco.json" />
</ItemGroup>
```

Если вы подключаете генераторы из исходников, используйте ссылку на проект анализатора.

Добавьте в проект, в котором должны появиться наследники, ссылку на генератор и файлы спецификаций.

```xml
<ItemGroup>
  <ProjectReference Include="../DuckiDuckiCastingLib/DuckiDuckiCastingLib.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
  <AdditionalFiles Include="PocoJson/Stages/**/*.poco.json" />
</ItemGroup>
```

Укажите тип прототипа атрибутом на базовом классе. Объявление атрибута добавляет генератор.

```csharp
[DuckiDuckiCastingLib.Annotations.PocoPrototype(typeof(AutoStageProto))]
public abstract class AutoStage
{
    // Члены базового класса.
}
```

В `Stages/ThermalCycleStage.poco.json` находится спецификация для `AutoStage`. Имя прототипа она получает из атрибута. Существующие файлы `*.stage.json` также поддерживаются, если они включены в `AdditionalFiles`.

- `namespace`, `name` и `baseType` обязательны. Для базового класса укажите полное метадатное имя необобщённого абстрактного класса. Сгенерированный класс имеет доступность `public`.
- `properties` задаёт значения виртуальных или абстрактных свойств только с `get`. `methods` задаёт возвращаемые значения виртуальных или абстрактных методов без параметров, например `"ToString()": "Этап термоцикла"`.
- Поддерживаются строки, символы из односимвольных строк, логические значения, числа, имена элементов перечислений и `null` для ссылочных и nullable-типов. Диапазон чисел определяется типом возвращаемого значения.
- Если на указанном базовом классе нет `PocoPrototype`, генератор берёт полное метадатное имя прототипа из поля `prototype`. Если отсутствуют оба источника, класс генерируется без атрибутов прототипа. В строковых аргументах атрибутов выбранного прототипа генератор подставляет переменные из `variables`. Поддерживаются `$name` и `${name}`, например `"auto_${stage_contract}_v2"`. Подставленные значения обрабатываются как текст. В значениях свойств и методов доллар является обычным символом.
- Ручные части `partial` могут реализовывать оставшиеся члены. Если после объединения частей остаются абстрактные члены, класс генерируется с модификатором `abstract`. JSON и ручная часть должны объявлять каждый член только один раз.
- Ошибки спецификации выдаются как `POCO001` с привязкой к началу соответствующего JSON-файла.

## Константы прототипа

Генератор переносит поля `const`, объявленные в прототипе, сохраняя имя, тип и доступность.
Обычное значение берётся из C#. В строковой константе можно использовать шаблон `$name` или `${name}`.

Чтобы взять всё значение из JSON, пометьте константу атрибутом `PocoVariable` из `DuckiDuckiCastingLib.Annotations`:

```csharp
public const string Contract = "$stage_contract";

[PocoVariable("retry_limit")]
public const int RetryLimit = 0;
```

```json
"variables": {
  "stage_contract": "thermal_cycle",
  "retry_limit": 3
}
```

Для `PocoVariable` укажите значение совместимого JSON-типа. Например, `3` для `int`, `true` для `bool` или строку с именем элемента перечисления. При наличии атрибута значение из JSON обязательно, а инициализатор в C# нужен для объявления константы. Для подстановок внутри строковых шаблонов подходят строковые переменные.

## Запуск примера

```sh
dotnet run --project DuckiDuckiSampleProject
```

Пример проверяет свойства, методы, подстановку контракта, неполную спецификацию и ручной `partial`. Атрибуты fastJSON в примере имитируют интерфейс атрибутов TTS. Полиморфную сериализацию и десериализацию нужно проверять в проекте TTS с реальным `FastJsonHelper.CreatePolymorphicParameters`.
