window.posPrintJob = async function (job) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/print/job", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(job)
        });
        if (!response.ok) {
            console.error("Local print job failed", response.statusText);
            return false;
        }
        const data = await response.json();
        return data.success === true;
    } catch (err) {
        console.error("Cannot connect to local hardware agent for print job", err);
        return false;
    }
};

window.posCheckPrinterStatus = async function (target) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/printer/status", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ target: target })
        });
        if (response.ok) {
            const data = await response.json();
            return data.isOnline === true;
        }
        return false;
    } catch (err) {
        console.error("Cannot connect to local hardware agent for status check", err);
        return false;
    }
};

window.posGetInstalledPrinters = async function () {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/devices/printers");
        if (response.ok) {
            return await response.json();
        }
        return [];
    } catch (err) {
        console.error("Cannot fetch installed printers from local hardware agent", err);
        return [];
    }
};

window.posGetSerialPorts = async function () {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/devices/ports");
        if (response.ok) {
            return await response.json();
        }
        return [];
    } catch (err) {
        console.error("Cannot fetch serial ports from local hardware agent", err);
        return [];
    }
};

window.posPrintText = async function (ip, port, text, encodingCodePage) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/print/text", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                ip: ip,
                port: port,
                text: text,
                encodingCodePage: encodingCodePage
            })
        });
        if (!response.ok) {
            console.error("Local printing failed", response.statusText);
        }
    } catch (err) {
        console.error("Cannot connect to local hardware agent", err);
    }
};

window.posPrintRaw = async function (ip, port, dataBase64) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/print/raw", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                ip: ip,
                port: port,
                dataBase64: dataBase64
            })
        });
        if (!response.ok) console.error("Local raw printing failed", response.statusText);
    } catch (err) {
        console.error("Cannot connect to local hardware agent", err);
    }
};

window.posPrintImage = async function (ip, port, imageBase64, maxWidth) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/print/image", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                ip: ip,
                port: port,
                imageBase64: imageBase64,
                maxWidth: maxWidth
            })
        });
        if (!response.ok) console.error("Local image printing failed", response.statusText);
    } catch (err) {
        console.error("Cannot connect to local hardware agent", err);
    }
};

window.posPrintBarcode = async function (ip, port, data, barcodeType, height) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/print/barcode", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                ip: ip,
                port: port,
                data: data,
                barcodeType: barcodeType,
                height: height
            })
        });
        if (!response.ok) console.error("Local barcode printing failed", response.statusText);
    } catch (err) {
        console.error("Cannot connect to local hardware agent", err);
    }
};

window.posPrintQr = async function (ip, port, data, moduleSize, errorLevel) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/print/qr", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                ip: ip,
                port: port,
                data: data,
                moduleSize: moduleSize,
                errorLevel: errorLevel
            })
        });
        if (!response.ok) console.error("Local QR printing failed", response.statusText);
    } catch (err) {
        console.error("Cannot connect to local hardware agent", err);
    }
};

window.posOpenDrawer = async function (ip, port) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/drawer/open", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                ip: ip,
                port: port
            })
        });
        if (!response.ok) console.error("Local drawer open failed", response.statusText);
    } catch (err) {
        console.error("Cannot connect to local hardware agent", err);
    }
};

window.posReadWeight = async function (port, baud, protocol) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/scale/weight", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                port: port,
                baudRate: baud,
                protocol: protocol
            })
        });
        if (response.ok) {
            return await response.json();
        } else {
            return {
                weight: 0.0,
                unit: "kg",
                isStable: false,
                success: false,
                errorMessage: "HTTP error status: " + response.status
            };
        }
    } catch (err) {
        console.error("Scale read connection error", err);
        return {
            weight: 0.0,
            unit: "kg",
            isStable: false,
            success: false,
            errorMessage: "Cannot connect to local hardware agent: " + err.message
        };
    }
};

window.posProcessPayment = async function (amount, reference, transactionType, protocol, port) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/payment/process", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                amount: amount,
                reference: reference,
                transactionType: transactionType,
                protocol: protocol,
                port: port
            })
        });
        if (response.ok) {
            return await response.json();
        } else {
            return {
                success: false,
                transactionId: "",
                authorizationCode: "",
                brand: "",
                lastFour: "",
                message: "HTTP error status: " + response.status,
                errorCode: "HTTP_ERROR"
            };
        }
    } catch (err) {
        console.error("Payment connection error", err);
        return {
            success: false,
            transactionId: "",
            authorizationCode: "",
            brand: "",
            lastFour: "",
            message: "Cannot connect to local hardware agent: " + err.message,
            errorCode: "CONNECTION_ERROR"
        };
    }
};

window.posCancelPayment = async function (transactionId, protocol, port) {
    try {
        const response = await fetch("http://127.0.0.1:9000/api/payment/cancel", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                transactionId: transactionId,
                protocol: protocol,
                port: port
            })
        });
        if (response.ok) {
            return await response.json();
        } else {
            return {
                success: false,
                transactionId: transactionId,
                authorizationCode: "",
                brand: "",
                lastFour: "",
                message: "HTTP error status: " + response.status,
                errorCode: "HTTP_ERROR"
            };
        }
    } catch (err) {
        console.error("Cancel payment connection error", err);
        return {
            success: false,
            transactionId: transactionId,
            authorizationCode: "",
            brand: "",
            lastFour: "",
            message: "Cannot connect to local hardware agent: " + err.message,
            errorCode: "CONNECTION_ERROR"
        };
    }
};
