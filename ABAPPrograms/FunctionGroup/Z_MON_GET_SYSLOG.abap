FUNCTION Z_MON_GET_SYSLOG.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IV_WINDOW_MINUTES)  TYPE  I  DEFAULT 30
*"     VALUE(IV_MIN_CLASS)       TYPE  C  DEFAULT 'E' "(A=Abort/E=Error/W=Warn/I=Info)
*"     VALUE(IV_MAX_ROWS)        TYPE  I  DEFAULT 200
*"  EXPORTING
*"     VALUE(EV_ABORT_COUNT)     TYPE  I
*"     VALUE(EV_ERROR_COUNT)     TYPE  I
*"     VALUE(EV_WARN_COUNT)      TYPE  I
*"     VALUE(EV_STATUS)          TYPE  C
*"     VALUE(EV_MESSAGE)         TYPE  STRING
*"  TABLES
*"     ET_SYSLOG_ENTRIES         TYPE  TY_SYSLOG_ENTRIES
*"  EXCEPTIONS
*"     SYSTEM_ERROR              1
*"----------------------------------------------------------------------
*
* RFC-enabled: YES
* Description: Reads SAP System Log (SM21) entries via
*              RSLG_READ_REMOTE_SYSLOG or direct SYSLOG FM calls.
*
* Syslog class hierarchy: A (Abort) > E (Error) > W (Warning) > I (Info)
*
* Authorization: S_ADMI_FCD with value SLGR (system log)
*----------------------------------------------------------------------

  CONSTANTS:
    lc_warn_abort TYPE i VALUE 1,
    lc_warn_error TYPE i VALUE 10.

  DATA: ls_entry  TYPE ty_syslog_entry,
        lv_from_dt TYPE d,
        lv_from_tm TYPE t.

  CLEAR: et_syslog_entries, ev_abort_count, ev_error_count,
         ev_warn_count, ev_status, ev_message.

  DATA(lv_minutes) = COND #( WHEN iv_window_minutes > 0
                             THEN iv_window_minutes ELSE 30 ).

  DATA(lv_min_class) = COND #( WHEN iv_min_class IS NOT INITIAL
                               THEN iv_min_class ELSE 'E' ).

  DATA(lv_max) = COND #( WHEN iv_max_rows > 0 THEN iv_max_rows ELSE 200 ).

  TRY.
*   ── Calculate start of window ────────────────────────────────────────
    GET TIME STAMP FIELD DATA(lv_now_ts).
    DATA(lv_from_ts) = lv_now_ts - ( lv_minutes * 60 ).
    CONVERT TIME STAMP lv_from_ts TIME ZONE 'UTC'
      INTO DATE lv_from_dt TIME lv_from_tm.

*   ── Call RSLG_READ_REMOTE_SYSLOG ─────────────────────────────────────
*   This FM reads the syslog for the current or a remote instance.
*   Parameters vary slightly by release – use SLGL_DISPLAY as fallback.
    DATA: lt_syslog    TYPE STANDARD TABLE OF syslog WITH DEFAULT KEY,
          lv_read_from TYPE sy-datum,
          lv_read_time TYPE sy-uzeit.

    lv_read_from = lv_from_dt.
    lv_read_time = lv_from_tm.

    CALL FUNCTION 'RSLG_READ_REMOTE_SYSLOG'
      EXPORTING
        datum_von    = lv_read_from
        uzeit_von    = lv_read_time
        datum_bis    = sy-datum
        uzeit_bis    = sy-uzeit
        msgtypes     = 'AEW'           " Abort, Error, Warning
      TABLES
        syslog_tab   = lt_syslog
      EXCEPTIONS
        OTHERS       = 1.

    IF sy-subrc <> 0.
*     Fallback: try RSLG_SYSLOG_READ_2
      CALL FUNCTION 'RSLG_SYSLOG_READ_2'
        EXPORTING
          read_start_date = lv_read_from
          read_start_time = lv_read_time
          read_end_date   = sy-datum
          read_end_time   = sy-uzeit
        TABLES
          log             = lt_syslog
        EXCEPTIONS
          OTHERS          = 1.
    ENDIF.

*   ── Map results ──────────────────────────────────────────────────────
    DATA lv_row TYPE i.
    LOOP AT lt_syslog INTO DATA(ls_sl).
      lv_row = lv_row + 1.
      IF lv_row > lv_max. EXIT. ENDIF.

*     Filter by class
      CASE lv_min_class.
        WHEN 'A'.
          " Only Abort
          CHECK ls_sl-msgclass = 'A'.
        WHEN 'E'.
          " Abort or Error
          CHECK ls_sl-msgclass = 'A' OR ls_sl-msgclass = 'E'.
        WHEN 'W'.
          " Abort, Error, or Warning
          CHECK ls_sl-msgclass = 'A' OR ls_sl-msgclass = 'E'
             OR ls_sl-msgclass = 'W'.
        WHEN OTHERS.
          " All
      ENDCASE.

      CLEAR ls_entry.
      CONVERT DATE ls_sl-datum TIME ls_sl-uzeit
        TIME ZONE sy-zonlo
        INTO TIME STAMP ls_entry-log_time TIME ZONE 'UTC'.
      ls_entry-syslog_class = ls_sl-msgclass.
      ls_entry-msg_id       = ls_sl-msgno.
      ls_entry-msg_text     = ls_sl-msgtxt.
      ls_entry-terminal     = ls_sl-terminal.
      ls_entry-user_name    = ls_sl-user.
      ls_entry-trans_code   = ls_sl-tcode.

      APPEND ls_entry TO et_syslog_entries.

*     Count by class
      CASE ls_sl-msgclass.
        WHEN 'A'. ev_abort_count = ev_abort_count + 1.
        WHEN 'E'. ev_error_count = ev_error_count + 1.
        WHEN 'W'. ev_warn_count  = ev_warn_count + 1.
      ENDCASE.
    ENDLOOP.

*   ── Determine health status ──────────────────────────────────────────
    IF ev_abort_count >= lc_warn_abort.
      ev_status  = gc_unhealthy.
      ev_message = |{ ev_abort_count } ABORT message(s) in syslog |
                && |(last { lv_minutes } min)|.
    ELSEIF ev_error_count >= lc_warn_error.
      ev_status  = gc_degraded.
      ev_message = |{ ev_error_count } ERROR message(s) in syslog |
                && |(last { lv_minutes } min)|.
    ELSEIF ev_error_count > 0 OR ev_warn_count > 0.
      ev_status  = gc_degraded.
      ev_message = |Syslog: { ev_error_count } error(s), |
                && |{ ev_warn_count } warning(s) in last { lv_minutes } min|.
    ELSE.
      ev_status  = gc_healthy.
      ev_message = |System log clean in last { lv_minutes } min|.
    ENDIF.

  CATCH cx_root INTO DATA(lx).
    ev_status  = gc_unhealthy.
    ev_message = lx->get_text( ).
    RAISE system_error.
  ENDTRY.

ENDFUNCTION.
