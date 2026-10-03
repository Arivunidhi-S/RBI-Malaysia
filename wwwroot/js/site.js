window.viewReport = function (url) {
    window.open(url, "_blank");
};

//window.showStudentReport = function (url) {
//    const frame = document.getElementById("studentReportFrame");

//    if (frame) {
//        frame.src = url;
//    }
//};

window.showReport = async function (url) {
    const frame = document.getElementById("ReportFrame");
    if (!frame) return;

    try {
        console.log("PDF-ஐ லோடு செய்ய முயற்சிக்கிறது: " + url);

        // 1. API-ல் இருந்து PDF பைனரி தரவை fetch செய்கிறது
        const response = await fetch(url);

        if (!response.ok) {
            throw new Error(`API பிழை! Status: ${response.status}`);
        }

        const blob = await response.blob();
        console.log("PDF தரவு பெறப்பட்டது, அளவு: " + blob.size + " bytes");

        if (blob.size === 0) {
            console.warn("எச்சரிக்கை: PDF டேட்டா காலியாக (0 bytes) உள்ளது!");
        }

        // 2. பிரவுசருக்குள் ஒரு தற்காலிக Blob URL-ஐ உருவாக்குகிறது
        const blobUrl = URL.createObjectURL(blob);

        // 3. அதை iframe-க்கு வழங்குகிறது
        frame.src = blobUrl;
    } catch (error) {
        console.error("PDF-ஐ iframe-ல் ஏற்றுவதில் பிழை:", error);
    }
};

//---------------For Popup Modal----------------


window.showReportPopup = async function (url) {
    document.getElementById("popupModal").style.display = "flex";
    const frame = document.getElementById("ReportFrame");
    if (!frame) return;

    try {
        console.log("PDF-ஐ லோடு செய்ய முயற்சிக்கிறது: " + url);

        // 1. API-ல் இருந்து PDF பைனரி தரவை fetch செய்கிறது
        const response = await fetch(url);

        if (!response.ok) {
            throw new Error(`API பிழை! Status: ${response.status}`);
        }

        const blob = await response.blob();
        console.log("PDF தரவு பெறப்பட்டது, அளவு: " + blob.size + " bytes");

        if (blob.size === 0) {
            console.warn("எச்சரிக்கை: PDF டேட்டா காலியாக (0 bytes) உள்ளது!");
        }

        // 2. பிரவுசருக்குள் ஒரு தற்காலிக Blob URL-ஐ உருவாக்குகிறது
        const blobUrl = URL.createObjectURL(blob);

        // 3. அதை iframe-க்கு வழங்குகிறது
        frame.src = blobUrl;
    } catch (error) {
        console.error("PDF-ஐ iframe-ல் ஏற்றுவதில் பிழை:", error);
    }
};
function openPopup(pageUrl) {

    document.getElementById("popupModal").style.display = "flex";

    document.getElementById("popupFrame").src = pageUrl;
}

function closePopup() {

    document.getElementById("popupModal").style.display = "none";

    document.getElementById("popupFrame").src = "";
}

// Close when click outside
window.onclick = function (event) {

    let modal = document.getElementById("popupModal");

    if (event.target == modal) {
        closePopup();
    }
}