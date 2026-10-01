lathe-menu-title = Інтерфейс Стату
lathe-menu-queue = Черга
lathe-menu-server-list = Список Серверів
lathe-menu-sync = Синхронізувати
lathe-menu-search-designs = Пошук предметів
lathe-menu-category-all = Все
lathe-menu-search-filter = Фільтр:
lathe-menu-amount = Кількість:
lathe-menu-recipe-count = { $count ->
    [1] {$count} Рецепт
    *[other] {$count} Рецептів
}
lathe-menu-reagent-slot-examine = Це має слот для колби.
lathe-reagent-dispense-no-container = Рідина виливається з {$name} на підлогу!
lathe-menu-result-reagent-display = {$reagent} ({$amount}од)
lathe-menu-material-display = {$material} ({$amount})
lathe-menu-tooltip-display = {$amount} {$material}
lathe-menu-description-display = [italic]{$description}[/italic]
lathe-menu-material-amount = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit}
    *[other] {NATURALFIXED($amount, 2)} {$unit}а
}
lathe-menu-material-amount-missing = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit} {$material} ([color=red]{NATURALFIXED($missingAmount, 2)} {$unit} не вистачає[/color])
    *[other] {NATURALFIXED($amount, 2)} {$unit}а {$material} ([color=red]{NATURALFIXED($missingAmount, 2)} {$unit}а не вистачає[/color])
}
lathe-menu-no-materials-message = Матеріали відсутні.
lathe-menu-silo-linked-message = Сіло Підключено
lathe-menu-fabricating-message = Виробляється...
lathe-menu-materials-title = Матеріали
lathe-menu-queue-title = Черга Вироблення
