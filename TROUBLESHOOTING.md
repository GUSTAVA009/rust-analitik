# 🔧 Устранение проблем с Sleeping Bag Labels

## 🚨 Если метки не отображаются

### 1️⃣ Проверьте права доступа
```bash
# В консоли сервера выполните:
oxide.grant user <SteamID> sleepingbaglabels.use

# Или дайте всем игрокам:
oxide.grant group default sleepingbaglabels.use
```

### 2️⃣ Проверьте настройки игрока
```
/sleepingbag status
```
Убедитесь что:
- Labels: ✅ ENABLED 
- Global Settings: ✅ ON

### 3️⃣ Используйте команду toggle
```
/sleepingbag toggle
```
Должно показать: "✅ Sleeping bag labels are now ENABLED!"

### 4️⃣ Проверьте расстояние
```
/sleepingbag distance 100
```
Установите большее расстояние для тестирования

---

## 🔍 Отладка для администраторов

### Проверка состояния плагина
```
/sleepingbag debug
```
Покажет:
- Количество активных меток
- Количество спальных мешков поблизости
- Версию плагина

### Принудительное обновление
```
/sleepingbag refresh
```
Пересоздаст все метки заново

### Проверка в консоли
```bash
oxide.plugins
```
Убедитесь что плагин загружен и активен

---

## ⚠️ Частые проблемы

### "Label toggle functionality will be implemented in future update!"
**Решение:** Обновите плагин до версии 1.0.3+

### Метки есть, но неправильного цвета
**Причина:** Игроки не состоят в команде
**Решение:** Создайте команду в игре или проверьте логику команд

### Метки исчезают при движении
**Причина:** Включена настройка "Show only when looking at bag"
**Решение:** Отключите в конфиге: `"Show only when looking at bag": false`

### Ошибки шейдера в логах
**Решение:** Используйте упрощенную версию `SleepingBagLabels_Simple.cs`

---

## 📋 Пошаговая диагностика

### Шаг 1: Основные проверки
1. ✅ Плагин загружен: `oxide.plugins`
2. ✅ Права есть: `oxide.usergroup show <username>`
3. ✅ Конфиг правильный: проверьте `SleepingBagLabels.json`

### Шаг 2: Игровые команды
1. ✅ `/sleepingbag` - показывает справку
2. ✅ `/sleepingbag status` - показывает настройки
3. ✅ `/sleepingbag toggle` - переключает метки

### Шаг 3: Административные команды
1. ✅ `/sleepingbag debug` - диагностика
2. ✅ `/sleepingbag refresh` - пересоздание
3. ✅ `/sleepingbag reload` - перезагрузка конфига

### Шаг 4: Тестирование
1. ✅ Подойдите к спальному мешку
2. ✅ Проверьте расстояние (должно быть < настроенного)
3. ✅ Убедитесь что метка появилась

---

## 🛠️ Сброс настроек

### Сброс настроек игрока
Настройки игрока сбрасываются при перезагрузке сервера.

### Сброс конфигурации
```bash
# Удалите файл конфигурации
rm oxide/config/SleepingBagLabels.json

# Перезагрузите плагин
oxide.reload SleepingBagLabels
```

### Полная переустановка
```bash
# Выгрузите плагин
oxide.unload SleepingBagLabels

# Удалите старые файлы
rm oxide/plugins/SleepingBagLabels.cs
rm oxide/config/SleepingBagLabels.json

# Установите новую версию
# (скопируйте новый файл)

# Загрузите плагин
oxide.load SleepingBagLabels
```

---

## 📞 Получение помощи

### Соберите информацию:
1. **Версия плагина**: `/sleepingbag debug`
2. **Версия сервера**: `version` в консоли
3. **Ошибки в логах**: проверьте последние логи
4. **Конфигурация**: содержимое `SleepingBagLabels.json`

### Полезные команды для отчета:
```bash
# Информация о сервере
version
oxide.version

# Статус плагина
oxide.show SleepingBagLabels
oxide.plugins | grep SleepingBag

# Проверка прав
oxide.usergroup show <username>
```

---

<div align="center">

**🎯 Большинство проблем решается командой `/sleepingbag toggle`**

*Если проблема остается - используйте `/sleepingbag debug` для диагностики*

</div>