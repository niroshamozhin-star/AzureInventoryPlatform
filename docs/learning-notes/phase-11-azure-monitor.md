# Phase 11 — Azure Monitor

## AZ-204 objectives covered
- Implement Azure Monitor alerts (metric alerts, action groups)

## 1. Business scenario

Application Insights (Phase 10) gives visibility, but only if someone is
actually looking at a dashboard. Azure Monitor turns that visibility into
a push notification - a metric alert watches the same telemetry Phase 10
already produces, and an action group tells it who to notify when a
threshold is crossed. No code, zero new instrumentation - this phase is
pure Azure configuration layered on top of what already exists.

## 2. Flow

```
Real traffic on the live site
      -> Application Insights records "requests/duration" for every request
      -> Azure Monitor alert rule evaluates the AVERAGE of that metric
         every 5 minutes, over a 5-minute lookback window
      -> average > 500ms?
            no  -> nothing happens, check again in 5 minutes
            yes -> alert rule fires
                     -> action group "niro-inventory-alerts" triggers
                          -> Email action sends a notification
      -> if average later drops back under 500ms, the alert
         auto-resolves (no manual close needed)
```

## 3. Azure resources created

- **Metric alert rule** `High server response time`, scoped to the shared
  Application Insights resource (`niro-inventory-functions`).
  - Signal: **Server response time** (underlying metric name: `requests/duration`)
  - Aggregation: **Average**, Operator: **Greater than**, Threshold: **500 milliseconds**
  - Evaluation: check every 5 minutes, 5-minute lookback window
  - Severity: **Sev 2 - Warning** (not Critical - nothing is actually down,
    just slower than ideal; not Informational either - it's a real signal
    worth looking at, not just noise)
  - Cost: $0.10/month
- **Action group** `niro-inventory-alerts` (display name `InvAlerts`) - one
  action: **Email**, to a real inbox. Two other quick-action options
  (Email Azure Resource Manager Role, Azure mobile app notification) were
  offered by the wizard and deliberately left unchecked - unnecessary
  complexity for a single-person test.

## 4. Azure Portal configuration

1. Application Insights resource (`niro-inventory-functions`) → **Alerts** → **Create alert rule**.
2. **Scope**: pre-filled as the App Insights resource itself.
3. **Condition** → signal **Server response time** → Aggregation Average,
   Greater than, 500 milliseconds → check every 5 min, lookback 5 min.
4. **Actions** → Use action groups → create new: name `niro-inventory-alerts`,
   display name `InvAlerts` → check **Email** only, enter address → uncheck
   the other two quick-action options → Save.
5. **Details** → Severity **2 - Warning** → name `High server response time`
   → confirm **Enable upon creation** and **Automatically resolve alerts**
   are both checked.
6. **Review + create** → **Create**.

## 5. Testing

**Verified live, full round trip, with real numbers:**
1. Browsed the live site (Dashboard, Products, Inventory, adjusted a
   quantity) to generate genuine traffic - no synthetic/forced failure.
2. Received Azure's automatic action-group confirmation email
   ("You've been added to the InvAlerts action group") shortly after
   creating it.
3. ~10-15 minutes later, the alert fired for real: **measured average
   504.91ms, threshold 500ms** - a narrow, believable margin from actual
   production data, not a forced trigger.
4. The fired email included the full metric detail: signal name
   `requests/duration`, aggregation `Average`, operator `GreaterThan`,
   metric value `504.9137`, threshold `500` - exact numbers, not just
   "it worked."
5. Confirmed in the Portal's Alerts blade that the same fire event shows
   there too, correlated with the email.

---
**Previous phase:** [Phase 10 — Application Insights](phase-10-application-insights.md)
**Next phase:** [Phase 12 & 13 — Docker and Container Registry](phase-12-13-docker-acr.md)
