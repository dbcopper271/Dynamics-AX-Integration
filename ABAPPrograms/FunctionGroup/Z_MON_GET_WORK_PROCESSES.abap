FUNCTION Z_MON_GET_WORK_PROCESSES.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  EXPORTING
*"     VALUE(EV_TOTAL_WP)       TYPE  I
*"     VALUE(EV_FREE_WP)        TYPE  I
*"     VALUE(EV_FREE_DIA_PCT)   TYPE  P  DECIMALS 2
*"     VALUE(EV_STATUS)         TYPE  C  "(HEALTHY/DEGRADED/UNHEALTHY)
*"     VALUE(EV_MESSAGE)        TYPE  STRING
*"  TABLES
*"     ET_WORK_PROCESSES        TYPE  TY_WORK_PROCESSES
*"  EXCEPTIONS
*"     SYSTEM_ERROR             1
*"----------------------------------------------------------------------
*
* RFC-enabled: YES
* Description: Returns SAP work process availability details.
*              Uses TH_WPINFO to read current WP states.
*
* Thresholds (mapped from appsettings.json MinFreeWorkProcessPercent):
*   < 20% free DIA → DEGRADED
*   < 10% free DIA → UNHEALTHY
*----------------------------------------------------------------------

  CONSTANTS:
    lc_threshold_warn TYPE p DECIMALS 2 VALUE '20.00',
    lc_threshold_crit TYPE p DECIMALS 2 VALUE '10.00'.

  DATA: lt_wpinfo   TYPE STANDARD TABLE OF wpinfo,
        ls_wpinfo   TYPE wpinfo,
        ls_wp       TYPE ty_work_process,
        lv_total    TYPE i,
        lv_free_dia TYPE i,
        lv_total_dia TYPE i.

  CLEAR: et_work_processes, ev_total_wp, ev_free_wp,
         ev_free_dia_pct, ev_status, ev_message.

  TRY.
*   ── Read work process info via TH_WPINFO ───────────────────────────
    CALL FUNCTION 'TH_WPINFO'
      TABLES
        wplist = lt_wpinfo
      EXCEPTIONS
        OTHERS = 1.

    IF sy-subrc <> 0.
      ev_status  = gc_unhealthy.
      ev_message = 'TH_WPINFO call failed – cannot read work processes'.
      RAISE system_error.
    ENDIF.

*   ── Map WP info to monitoring structure ────────────────────────────
    LOOP AT lt_wpinfo INTO ls_wpinfo.

      lv_total = lv_total + 1.

      CLEAR ls_wp.
      ls_wp-wp_no       = ls_wpinfo-wp_no.
      ls_wp-wp_pid      = ls_wpinfo-wp_pid.
      ls_wp-wp_program  = ls_wpinfo-wp_prog.
      ls_wp-wp_user     = ls_wpinfo-wp_user.
      ls_wp-wp_client   = ls_wpinfo-wp_mand.
      ls_wp-wp_cpu_time = ls_wpinfo-wp_cpu.
      ls_wp-wp_elapsed  = ls_wpinfo-wp_elaps.
      ls_wp-wp_semaphore = ls_wpinfo-wp_semno.

*     Map WP type code
      CASE ls_wpinfo-wp_typ.
        WHEN 'DIA'. ls_wp-wp_type = gc_wp_dia.
        WHEN 'BTC'. ls_wp-wp_type = gc_wp_btc.
        WHEN 'SPO'. ls_wp-wp_type = gc_wp_spo.
        WHEN 'UPD'. ls_wp-wp_type = gc_wp_upd.
        WHEN 'UP2'. ls_wp-wp_type = gc_wp_up2.
        WHEN 'ENQ'. ls_wp-wp_type = gc_wp_enq.
        WHEN OTHERS. ls_wp-wp_type = ls_wpinfo-wp_typ(2).
      ENDCASE.

*     Map WP status
      CASE ls_wpinfo-wp_status.
        WHEN 'Wait'.  ls_wp-wp_status = 'Wait'.
        WHEN 'Run'.   ls_wp-wp_status = 'Run'.
        WHEN 'Hold'.  ls_wp-wp_status = 'Hold'.
        WHEN 'Stop'.  ls_wp-wp_status = 'Stop'.
        WHEN 'Ended'. ls_wp-wp_status = 'Ended'.
        WHEN OTHERS.  ls_wp-wp_status = ls_wpinfo-wp_status.
      ENDCASE.

*     Count free dialog WPs
      IF ls_wp-wp_type = gc_wp_dia.
        lv_total_dia = lv_total_dia + 1.
        IF ls_wpinfo-wp_status = 'Wait'.
          lv_free_dia = lv_free_dia + 1.
          ev_free_wp  = ev_free_wp + 1.
        ENDIF.
      ENDIF.

      APPEND ls_wp TO et_work_processes.
    ENDLOOP.

    ev_total_wp = lv_total.

*   ── Calculate free DIA percentage ──────────────────────────────────
    IF lv_total_dia > 0.
      ev_free_dia_pct = ( lv_free_dia * 100 ) / lv_total_dia.
    ELSE.
      ev_free_dia_pct = 0.
    ENDIF.

*   ── Determine health status ─────────────────────────────────────────
    IF ev_free_dia_pct < lc_threshold_crit.
      ev_status  = gc_unhealthy.
      ev_message = |Critical: only { ev_free_dia_pct }% DIA WPs free |
                && |({ lv_free_dia }/{ lv_total_dia })|.
    ELSEIF ev_free_dia_pct < lc_threshold_warn.
      ev_status  = gc_degraded.
      ev_message = |Warning: { ev_free_dia_pct }% DIA WPs free |
                && |({ lv_free_dia }/{ lv_total_dia })|.
    ELSE.
      ev_status  = gc_healthy.
      ev_message = |Work processes OK: { ev_free_dia_pct }% DIA free |
                && |({ lv_free_dia }/{ lv_total_dia }), |
                && |Total WP: { lv_total }|.
    ENDIF.

  CATCH cx_root INTO DATA(lx).
    ev_status  = gc_unhealthy.
    ev_message = lx->get_text( ).
    RAISE system_error.
  ENDTRY.

ENDFUNCTION.
