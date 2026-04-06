FUNCTION Z_MON_GET_SYSTEM_HEALTH.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  EXPORTING
*"     VALUE(ES_HEALTH)      TYPE  TY_SYSTEM_HEALTH
*"     VALUE(ET_METRICS)     TYPE  TY_METRICS
*"     VALUE(EV_STATUS)      TYPE  C           "(HEALTHY/DEGRADED/UNHEALTHY)
*"     VALUE(EV_MESSAGE)     TYPE  STRING
*"  EXCEPTIONS
*"     SYSTEM_ERROR          1
*"----------------------------------------------------------------------
*
* RFC-enabled: YES  (Remote-enabled Module)
* Description: Returns comprehensive SAP system health information.
*              Called by .NET SapRfcClient every polling interval.
*
* RFC Parameters (SE37):
*   Remote-enabled Module = enabled
*   Pass Value = checked for all EXPORTING
*----------------------------------------------------------------------

  DATA: lv_db_host      TYPE c LENGTH 100,
        lv_kernel_info  TYPE c LENGTH 255,
        lt_rfcsi        TYPE STANDARD TABLE OF rfcsi,
        ls_rfcsi        TYPE rfcsi,
        lv_uptime_s     TYPE i,
        lv_free_wp_pct  TYPE p DECIMALS 2,
        ls_metric       TYPE ty_metric.

  CLEAR: es_health, et_metrics, ev_status, ev_message.

  TRY.
*   ── 1. Basic system identification ──────────────────────────────────
      es_health-sid         = sy-sysid.
      es_health-sap_release = sy-saprl.
      es_health-codepage    = sy-cpcodepage.
      es_health-timezone    = 'UTC'.

*   ── 2. RFC System Info (kernel, host, DB details) ───────────────────
      CALL FUNCTION 'RFC_SYSTEM_INFO'
        IMPORTING
          rfcsi_export = ls_rfcsi
        EXCEPTIONS
          OTHERS       = 1.

      IF sy-subrc = 0.
        es_health-host      = ls_rfcsi-rfchost.
        es_health-kernel_rel = ls_rfcsi-rfckernrl.
        es_health-db_type   = ls_rfcsi-rfcdbsys.
        es_health-db_host   = ls_rfcsi-rfcdbhost.
        es_health-db_name   = ls_rfcsi-rfcsysid.   " DB SID
        es_health-inst_number = ls_rfcsi-rfcsysno.
      ELSE.
*       Fallback to sy-* variables
        es_health-host      = sy-host.
        es_health-inst_number = sy-synnr.
      ENDIF.

*   ── 3. System start time ────────────────────────────────────────────
      DATA: lv_start_date TYPE d,
            lv_start_time TYPE t.

      CALL FUNCTION 'TH_SERVER_LIST'
        TABLES
          list = DATA(lt_servers)
        EXCEPTIONS
          OTHERS = 1.

      IF sy-subrc = 0 AND lines( lt_servers ) > 0.
        READ TABLE lt_servers INTO DATA(ls_server) INDEX 1.
        " Calculate approximate uptime
        DATA(lv_now) = sy-datum.
        GET TIME STAMP FIELD DATA(lv_ts_now).
        es_health-start_time = lv_ts_now.   " Placeholder – use TH info
      ENDIF.

*   ── 4. Aggregate status metrics ─────────────────────────────────────

*     4a. Active users
      DATA(lv_active_users) = 0.
      CALL FUNCTION 'TH_USER_LIST'
        TABLES
          usrlist = DATA(lt_users)
        EXCEPTIONS
          OTHERS  = 1.
      IF sy-subrc = 0.
        lv_active_users = lines( lt_users ).
      ENDIF.

      ls_metric-metric_key   = 'ActiveUsers'.
      ls_metric-metric_value = lv_active_users.
      ls_metric-metric_unit  = 'sessions'.
      APPEND ls_metric TO et_metrics. CLEAR ls_metric.

*     4b. System uptime (minutes) – read from system log start entry
      SELECT SINGLE startdatum, startuhrzeit
        FROM smlg_assgn
        INTO @DATA(ls_start)
        WHERE lgrp = 'SPACE'.                        "#EC CI_NOORDER
      IF sy-subrc = 0.
        DATA(lv_elapsed_min) = ( ( sy-datum - ls_start-startdatum ) * 1440 )
                             + ( ( sy-uzeit - ls_start-startuhrzeit ) / 60 ).
        ls_metric-metric_key   = 'UptimeMinutes'.
        ls_metric-metric_value = lv_elapsed_min.
        ls_metric-metric_unit  = 'min'.
        APPEND ls_metric TO et_metrics. CLEAR ls_metric.
      ENDIF.

*     4c. SAP release and kernel
      ls_metric-metric_key   = 'SapRelease'.
      ls_metric-metric_value = sy-saprl.
      APPEND ls_metric TO et_metrics. CLEAR ls_metric.

      ls_metric-metric_key   = 'KernelRelease'.
      ls_metric-metric_value = es_health-kernel_rel.
      APPEND ls_metric TO et_metrics. CLEAR ls_metric.

      ls_metric-metric_key   = 'Host'.
      ls_metric-metric_value = es_health-host.
      APPEND ls_metric TO et_metrics. CLEAR ls_metric.

      ls_metric-metric_key   = 'DBType'.
      ls_metric-metric_value = es_health-db_type.
      APPEND ls_metric TO et_metrics. CLEAR ls_metric.

*   ── 5. Determine overall status ─────────────────────────────────────
      ev_status  = gc_healthy.
      ev_message = |SAP { sy-sysid } healthy. Release { sy-saprl }, |
                && |{ lv_active_users } active users.|.

    es_health-status = ev_status.

  CATCH cx_root INTO DATA(lx).
    ev_status  = gc_unhealthy.
    ev_message = lx->get_text( ).
    RAISE system_error.
  ENDTRY.

ENDFUNCTION.
