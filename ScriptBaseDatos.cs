/*
 
-- =========================
-- BASE DE DATOS Y TABLAS
-- =========================
CREATE DATABASE IF NOT EXISTS swgroi_db
  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE swgroi_db;

CREATE TABLE IF NOT EXISTS usuarios (
  IdUsuario INT NOT NULL AUTO_INCREMENT,
  NombreCompleto VARCHAR(100),
  Usuario VARCHAR(50) UNIQUE,
  Contrasena VARCHAR(100),
  Rol VARCHAR(50),
  PRIMARY KEY (IdUsuario)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS tickets (
  Id INT NOT NULL AUTO_INCREMENT,
  Folio VARCHAR(20) UNIQUE,
  Descripcion TEXT,
  Estado VARCHAR(50),
  Responsable VARCHAR(100),
  Tecnico VARCHAR(100),
  Cuenta VARCHAR(100),
  RazonSocial VARCHAR(150),
  Regional VARCHAR(100),
  Domicilio TEXT,
  FechaAtencion DATE,
  AgenteResponsable VARCHAR(100),
  FechaRegistro DATETIME DEFAULT CURRENT_TIMESTAMP,
  FechaAsignada DATE,
  HoraAsignada VARCHAR(10),
  FechaCierre DATETIME,
  Cotizacion VARCHAR(50),
  Comentario TEXT,
  ComentariosTecnico TEXT,
  PRIMARY KEY (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS tecnicos (
  IdTecnico INT NOT NULL AUTO_INCREMENT,
  Nombre VARCHAR(100),
  PRIMARY KEY (IdTecnico)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS asignaciones (
  AsignacionID INT NOT NULL AUTO_INCREMENT,
  TicketID INT,
  TecnicoID INT,
  FechaServicio DATE,
  HoraServicio TIME,
  PRIMARY KEY (AsignacionID),
  KEY (TicketID),
  KEY (TecnicoID),
  CONSTRAINT asignaciones_ibfk_1 FOREIGN KEY (TicketID) REFERENCES tickets (Id),
  CONSTRAINT asignaciones_ibfk_2 FOREIGN KEY (TecnicoID) REFERENCES tecnicos (IdTecnico)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS estadoscotizacion (
  EstadoCotizacionID INT NOT NULL AUTO_INCREMENT,
  Nombre VARCHAR(50),
  PRIMARY KEY (EstadoCotizacionID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Columna normalizada (compatible con versiones que no aceptan IF NOT EXISTS)
SET @have_col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'estadoscotizacion'
    AND COLUMN_NAME = 'NombreNorm'
);
SET @sql := IF(@have_col=0,
  'ALTER TABLE estadoscotizacion ADD COLUMN NombreNorm VARCHAR(50) GENERATED ALWAYS AS (UPPER(TRIM(Nombre))) STORED',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

CREATE TABLE IF NOT EXISTS cotizaciones (
  CotizacionID INT NOT NULL AUTO_INCREMENT,
  TicketID INT,
  EstadoCotizacionID INT,
  FechaEnvio DATE,
  Monto DECIMAL(12,2),
  Comentarios TEXT,
  PRIMARY KEY (CotizacionID),
  KEY (TicketID),
  KEY (EstadoCotizacionID),
  CONSTRAINT cotizaciones_ibfk_1 FOREIGN KEY (TicketID) REFERENCES tickets (Id),
  CONSTRAINT cotizaciones_ibfk_2 FOREIGN KEY (EstadoCotizacionID) REFERENCES estadoscotizacion (EstadoCotizacionID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS ordenesventa (
  OVSR3 VARCHAR(50) NOT NULL,
  CotizacionID INT,
  FechaVenta DATE,
  Comision DECIMAL(12,2),
  PRIMARY KEY (OVSR3),
  KEY (CotizacionID),
  CONSTRAINT ordenesventa_ibfk_1 FOREIGN KEY (CotizacionID) REFERENCES cotizaciones (CotizacionID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS ventasdetalle (
  DetalleID INT NOT NULL AUTO_INCREMENT,
  CotizacionID INT,
  OVSR3 VARCHAR(50),
  Fecha DATE,
  Cuenta VARCHAR(100),
  RazonSocial VARCHAR(150),
  Regional VARCHAR(100),
  Domicilio TEXT,
  Descripcion TEXT,
  Comentarios TEXT,
  FechaAtencion DATE,
  AgenteResponsable VARCHAR(100),
  Monto DECIMAL(12,2),
  Iva DECIMAL(12,2),
  TotalConComision DECIMAL(12,2),
  StatusPago VARCHAR(100),
  FechaCancelacion DATETIME,
  MotivoCancelacion VARCHAR(250),
  UsuarioCancelacion VARCHAR(100),
  ConstanciaDe VARCHAR(100),
  ComentariosCotizacion TEXT,
  PRIMARY KEY (DetalleID),
  UNIQUE KEY uq_ventasdetalle_ovsr3 (OVSR3),
  KEY (CotizacionID),
  KEY ix_ventasdetalle_statuspago (StatusPago),
  CONSTRAINT ventasdetalle_ibfk_1 FOREIGN KEY (CotizacionID) REFERENCES cotizaciones (CotizacionID),
  CONSTRAINT ventasdetalle_ibfk_2 FOREIGN KEY (OVSR3) REFERENCES ordenesventa (OVSR3)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS documentos (
  DocumentoID INT NOT NULL AUTO_INCREMENT,
  NombreArchivo VARCHAR(100),
  TipoMIME VARCHAR(50),
  FechaSubida DATE,
  UsuarioID INT,
  PRIMARY KEY (DocumentoID),
  KEY (UsuarioID),
  CONSTRAINT documentos_ibfk_1 FOREIGN KEY (UsuarioID) REFERENCES usuarios (IdUsuario)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS avisos (
  Id INT NOT NULL AUTO_INCREMENT,
  Fecha DATETIME DEFAULT CURRENT_TIMESTAMP,
  Asunto VARCHAR(100),
  Mensaje TEXT,
  MetricasID INT,
  PRIMARY KEY (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS metricas (
  Id INT NOT NULL AUTO_INCREMENT,
  TotalTickets INT,
  TicketsCerrados INT,
  PRIMARY KEY (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS retroalimentacion (
  RetroID INT NOT NULL AUTO_INCREMENT,
  Cliente VARCHAR(100),
  EnlaceUnico VARCHAR(255) UNIQUE,
  UsuarioID INT,
  PRIMARY KEY (RetroID),
  KEY (UsuarioID),
  CONSTRAINT retroalimentacion_ibfk_1 FOREIGN KEY (UsuarioID) REFERENCES usuarios (IdUsuario)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS serviciostecnicos (
  ServicioTecnicoID INT NOT NULL AUTO_INCREMENT,
  TicketID INT,
  Almacen VARCHAR(100),
  FechaCaptura DATE,
  FechaProgramacion DATE,
  FechaCierre DATE,
  Retroalimentacion TEXT,
  PRIMARY KEY (ServicioTecnicoID),
  KEY (TicketID),
  CONSTRAINT serviciostecnicos_ibfk_1 FOREIGN KEY (TicketID) REFERENCES tickets (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- =========================
-- VISTA ROBUSTA
-- =========================
CREATE OR REPLACE VIEW vistareporteventas AS
SELECT 
  o.OVSR3,
  c.CotizacionID,
  c.Monto AS MontoCotizado,
  o.FechaVenta,
  o.Comision,
  vd.Fecha,
  COALESCE(ec.Nombre,'ENVIADO') AS Estado,
  vd.Cuenta,
  vd.RazonSocial,
  vd.Regional,
  vd.Domicilio,
  vd.Descripcion,
  vd.FechaAtencion,
  vd.AgenteResponsable,
  vd.Monto,
  vd.StatusPago,
  vd.ConstanciaDe
FROM ordenesventa o
JOIN cotizaciones c            ON c.CotizacionID = o.CotizacionID
LEFT JOIN estadoscotizacion ec ON c.EstadoCotizacionID = ec.EstadoCotizacionID
JOIN ventasdetalle vd          ON vd.OVSR3 = o.OVSR3;

-- =========================
-- SEMILLAS Y NORMALIZACIÓN
-- =========================
INSERT IGNORE INTO estadoscotizacion (Nombre) VALUES
('ENVIADO'),('DECLINADA'),('ALMACEN'),
('OPERACIONES'),('COBRANZA'),('FACTURACION'),('FINALIZADO');

UPDATE cotizaciones c
JOIN estadoscotizacion e ON e.NombreNorm='ENVIADO'
SET c.EstadoCotizacionID = e.EstadoCotizacionID
WHERE c.EstadoCotizacionID IS NULL;

-- Usuario inicial
INSERT IGNORE INTO usuarios (NombreCompleto, Usuario, Contrasena, Rol)
VALUES ('Administrador General', 'admin', 'admin123', 'Administrador');


*/