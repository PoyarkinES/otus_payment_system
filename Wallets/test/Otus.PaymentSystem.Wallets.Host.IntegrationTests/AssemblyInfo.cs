using Xunit;

// Три класса интеграционных тестов поднимают собственные WebApplicationFactory и при
// параллельном запуске одновременно применяют миграции к одной базе. На пустой БД это
// гонка (42P07 "relation already exists", 42710 "constraint already exists"), поэтому
// классы выполняются последовательно: суммарно это несколько секунд.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
