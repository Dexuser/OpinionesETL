-- =====================================================================
-- 07_consultas_validacion_worker.sql
-- Consultas para comprobar el resultado de una ejecución del Worker.
-- Ejecutar en la base analítica (opiniones_clientes).
-- =====================================================================

-- 1) Opiniones por origen y sentimiento (debe coincidir con lo que reporta el resumen del Worker).
SELECT o.origen, COUNT(*) AS opiniones,
       SUM(es_positiva) AS positivas, SUM(es_neutra) AS neutras, SUM(es_negativa) AS negativas
  FROM dw.fact_opinion o
 GROUP BY o.origen ORDER BY o.origen;

-- 2) Cada fuente/canal con su volumen.
SELECT f.tipo_fuente, f.canal, COUNT(*) AS opiniones
  FROM dw.fact_opinion o JOIN dw.dim_fuente f USING (sk_fuente)
 GROUP BY 1, 2 ORDER BY 1, 2;

-- 3) Staging vs. almacén: lo que está en staging y NO llegó a dw (debe coincidir con los rechazos de integridad).
SELECT s.origen, COUNT(*) AS en_staging_sin_hecho
  FROM stg.opinion s
  LEFT JOIN dw.fact_opinion f ON f.origen = s.origen AND f.id_origen = s.id_origen
 WHERE f.sk_opinion IS NULL
 GROUP BY s.origen;

-- 4) Registros rechazados, por etapa, origen y motivo agrupado.
SELECT etapa, origen,
       regexp_replace(motivo, '[0-9]+', 'N', 'g') AS motivo_tipo,
       COUNT(*) AS cantidad
  FROM stg.rechazo
 GROUP BY 1, 2, 3 ORDER BY cantidad DESC;

-- 5) Detalle de rechazos de extracción (datos originales para corregirlos en la fuente).
SELECT origen, id_registro, motivo, datos_originales
  FROM stg.rechazo WHERE etapa = 'Extraccion' ORDER BY origen, id_registro;

-- 6) Integridad referencial: no debe haber hechos huérfanos (resultado esperado: 0).
SELECT COUNT(*) AS huerfanos
  FROM dw.fact_opinion o
  LEFT JOIN dw.dim_tiempo   t ON t.id_tiempo   = o.id_tiempo
  LEFT JOIN dw.dim_producto p ON p.sk_producto = o.sk_producto
  LEFT JOIN dw.dim_cliente  c ON c.sk_cliente  = o.sk_cliente
  LEFT JOIN dw.dim_fuente   f ON f.sk_fuente   = o.sk_fuente
 WHERE t.id_tiempo IS NULL OR p.sk_producto IS NULL OR c.sk_cliente IS NULL OR f.sk_fuente IS NULL;

-- 7) Idempotencia: no debe haber duplicados por (origen, id_origen) (resultado esperado: 0 filas).
SELECT origen, id_origen, COUNT(*) FROM dw.fact_opinion GROUP BY 1, 2 HAVING COUNT(*) > 1;

-- 8) Banderas de sentimiento: cada hecho suma exactamente 1 salvo "Sin clasificar" (esperado: 0 filas con suma > 1).
SELECT COUNT(*) AS banderas_invalidas
  FROM dw.fact_opinion WHERE es_positiva + es_neutra + es_negativa > 1;

-- 9) Satisfacción por mes (% de opiniones positivas) para un vistazo al valor analítico.
SELECT t.anio_mes, COUNT(*) AS opiniones,
       ROUND(100.0 * SUM(o.es_positiva) / COUNT(*), 1) AS pct_positivas
  FROM dw.fact_opinion o JOIN dw.dim_tiempo t USING (id_tiempo)
 GROUP BY t.anio_mes ORDER BY t.anio_mes;
